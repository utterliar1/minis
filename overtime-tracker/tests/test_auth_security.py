import hashlib
import importlib
import os
import sqlite3
import stat
import sys
import time as stdlib_time
from contextlib import contextmanager
from pathlib import Path

import pytest


BACKEND_DIR = Path(__file__).resolve().parents[1] / "backend"
MISSING = object()


@contextmanager
def load_app(monkeypatch, tmp_path):
    db_path = tmp_path / "overtime.db"
    monkeypatch.setenv("DB_PATH", str(db_path))
    monkeypatch.delenv("JWT_SECRET", raising=False)
    old_app = sys.modules.get("app", MISSING)
    old_path = list(sys.path)
    sys.modules.pop("app", None)
    backend_path = str(BACKEND_DIR)
    if backend_path not in sys.path:
        sys.path.insert(0, backend_path)

    try:
        module = importlib.import_module("app")
        module.app.config["TESTING"] = True
        yield module, db_path
    finally:
        sys.modules.pop("app", None)
        if old_app is not MISSING:
            sys.modules["app"] = old_app
        sys.path[:] = old_path


def fetch_user(db_path, username):
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    row = conn.execute("SELECT * FROM users WHERE username=?", (username,)).fetchone()
    conn.close()
    return row


def fetch_user_by_display_name(db_path, display_name):
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    row = conn.execute("SELECT * FROM users WHERE display_name=?", (display_name,)).fetchone()
    conn.close()
    return row


def test_default_admin_password_uses_bcrypt(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        admin = fetch_user(db_path, "admin")

        assert admin["password_hash"].startswith("$2b$")
        assert admin["salt"] == ""
        assert app_module.verify_pw("admin123", admin)


def test_legacy_sha256_user_migrates_to_bcrypt_after_login(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        legacy_salt = "legacy-salt"
        legacy_hash = hashlib.sha256(("secret123" + legacy_salt).encode()).hexdigest()
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("legacy", "Legacy User", legacy_hash, legacy_salt, "user", 1),
        )
        conn.commit()
        conn.close()

        response = app_module.app.test_client().post(
            "/api/login", json={"username": "legacy", "password": "secret123"}
        )

        assert response.status_code == 200
        migrated = fetch_user(db_path, "legacy")
        assert migrated["password_hash"].startswith("$2b$")
        assert migrated["salt"] == ""
        assert app_module.verify_pw("secret123", migrated)


def test_jwt_secret_file_is_reused_across_reimport(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (first_app, db_path):
        secret_file = db_path.parent / "jwt_secret"

        assert secret_file.exists()
        first_secret = first_app.SECRET
        assert secret_file.read_text(encoding="utf-8") == first_secret

    with load_app(monkeypatch, tmp_path) as (second_app, _):
        assert second_app.SECRET == first_secret
        assert secret_file.read_text(encoding="utf-8") == first_secret


def test_jwt_secret_file_is_created_with_exclusive_private_mode(monkeypatch, tmp_path):
    real_open = os.open
    calls = []

    def recording_open(path, flags, mode=0o777, *args, **kwargs):
        if Path(path).name == "jwt_secret":
            calls.append((flags, mode))
        return real_open(path, flags, mode, *args, **kwargs)

    monkeypatch.setattr(os, "open", recording_open)

    with load_app(monkeypatch, tmp_path) as (_, db_path):
        secret_file = db_path.parent / "jwt_secret"

        assert calls
        flags, mode = calls[0]
        assert flags & os.O_CREAT
        assert flags & os.O_EXCL
        assert flags & os.O_WRONLY
        assert mode == 0o600
        if os.name == "posix":
            assert stat.S_IMODE(secret_file.stat().st_mode) == 0o600


def test_jwt_secret_reads_existing_file_after_exclusive_create_race(monkeypatch, tmp_path):
    raced_secret = "raced-secret"
    real_open = os.open

    def racing_open(path, flags, mode=0o777, *args, **kwargs):
        if Path(path).name == "jwt_secret" and flags & os.O_EXCL:
            Path(path).write_text(raced_secret, encoding="utf-8")
            raise FileExistsError
        return real_open(path, flags, mode, *args, **kwargs)

    monkeypatch.setattr(os, "open", racing_open)

    with load_app(monkeypatch, tmp_path) as (app_module, _):
        assert app_module.SECRET == raced_secret


def test_jwt_secret_retries_empty_race_file_until_populated(monkeypatch, tmp_path):
    raced_secret = "winner-secret"
    secret_file = tmp_path / "jwt_secret"
    real_open = os.open
    sleeps = []

    def racing_open(path, flags, mode=0o777, *args, **kwargs):
        if Path(path).name == "jwt_secret" and flags & os.O_EXCL:
            secret_file.write_text("", encoding="utf-8")
            raise FileExistsError
        return real_open(path, flags, mode, *args, **kwargs)

    def fake_sleep(delay):
        sleeps.append(delay)
        if len(sleeps) == 3:
            secret_file.write_text(raced_secret, encoding="utf-8")

    monkeypatch.setattr(os, "open", racing_open)
    monkeypatch.setattr(stdlib_time, "sleep", fake_sleep)

    with load_app(monkeypatch, tmp_path) as (app_module, _):
        assert app_module.SECRET == raced_secret

    assert sleeps[:3] == [0.05, 0.05, 0.05]


def test_empty_jwt_secret_file_fails_closed_after_retries(monkeypatch, tmp_path):
    (tmp_path / "jwt_secret").write_text("", encoding="utf-8")
    sleeps = []
    monkeypatch.setattr(stdlib_time, "sleep", lambda delay: sleeps.append(delay))

    with pytest.raises(RuntimeError, match="JWT secret"):
        with load_app(monkeypatch, tmp_path):
            pass

    assert sleeps == [0.05] * 10


def test_existing_jwt_secret_is_chmodded_when_read(monkeypatch, tmp_path):
    secret_file = tmp_path / "jwt_secret"
    secret_file.write_text("persisted-secret", encoding="utf-8")
    chmod_calls = []

    def recording_chmod(path, mode):
        if Path(path).name == "jwt_secret":
            chmod_calls.append(mode)

    monkeypatch.setattr(os, "chmod", recording_chmod)

    with load_app(monkeypatch, tmp_path) as (app_module, _):
        assert app_module.SECRET == "persisted-secret"
        assert 0o600 in chmod_calls


def test_load_app_restores_import_state(monkeypatch, tmp_path):
    old_app = sys.modules.get("app", MISSING)
    old_path = list(sys.path)

    with load_app(monkeypatch, tmp_path):
        assert str(BACKEND_DIR) in sys.path

    assert sys.path == old_path
    if old_app is MISSING:
        assert "app" not in sys.modules
    else:
        assert sys.modules["app"] is old_app


class FakeConn:
    def __init__(self, row=None, raise_on_execute=False):
        self.row = row
        self.raise_on_execute = raise_on_execute
        self.closed = False
        self.rolled_back = False

    def execute(self, *args, **kwargs):
        if self.raise_on_execute:
            raise RuntimeError("db failed")
        return FakeCursor(self.row)

    def commit(self):
        pass

    def rollback(self):
        self.rolled_back = True

    def close(self):
        self.closed = True


class FakeCursor:
    def __init__(self, row):
        self.row = row

    def fetchone(self):
        return self.row


def test_api_login_closes_and_rolls_back_on_exception(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        conn = FakeConn({"username": "u", "password_hash": "h", "salt": "s"})
        monkeypatch.setattr(app_module, "get_db", lambda: conn)
        monkeypatch.setattr(app_module, "verify_pw", lambda *args: (_ for _ in ()).throw(RuntimeError("boom")))

        with app_module.app.test_request_context("/api/login", method="POST", json={"username": "u", "password": "p"}):
            with pytest.raises(RuntimeError):
                app_module.api_login()

        assert conn.rolled_back
        assert conn.closed


def test_api_register_closes_and_rolls_back_on_exception(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        conn = FakeConn(raise_on_execute=True)
        monkeypatch.setattr(app_module, "get_db", lambda: conn)

        with app_module.app.test_request_context("/api/register", method="POST", json={"displayName": "Alice", "password": "secret"}):
            with pytest.raises(RuntimeError):
                app_module.api_register()

        assert conn.rolled_back
        assert conn.closed


def test_api_change_password_closes_and_rolls_back_on_exception(monkeypatch, tmp_path):
    user = {"username": "u", "password_hash": "h", "salt": "s"}
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        conn = FakeConn(user)
        monkeypatch.setattr(app_module, "get_db", lambda: conn)
        monkeypatch.setattr(app_module, "verify_pw", lambda *args: (_ for _ in ()).throw(RuntimeError("boom")))

        with app_module.app.test_request_context("/api/change-password", method="PUT", json={"oldPassword": "old", "newPassword": "newpass"}):
            app_module.request.user = {"username": "u"}
            with pytest.raises(RuntimeError):
                app_module.api_change_password.__wrapped__()

        assert conn.rolled_back
        assert conn.closed


def test_api_reset_password_closes_and_rolls_back_on_exception(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        conn = FakeConn({"username": "u"})
        monkeypatch.setattr(app_module, "get_db", lambda: conn)
        monkeypatch.setattr(app_module, "hash_pw", lambda *args: (_ for _ in ()).throw(RuntimeError("boom")))

        with app_module.app.test_request_context("/api/users/u/password", method="PUT", json={"password": "newpass"}):
            with pytest.raises(RuntimeError):
                app_module.api_reset_password.__wrapped__("u")

        assert conn.rolled_back
        assert conn.closed


def test_login_required_closes_db_connection_when_auth_query_raises(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        conn = FakeConn(raise_on_execute=True)
        monkeypatch.setattr(app_module, "get_db", lambda: conn)

        @app_module.login_required
        def protected():
            return "ok"

        token = app_module.make_token({"username": "admin", "display_name": "管理员", "role": "admin"})
        with app_module.app.test_request_context("/", headers={"Authorization": f"Bearer {token}"}):
            response, status = protected()

        assert status == 401
        assert conn.closed


def test_register_success_stores_bcrypt_password_without_salt(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO whitelist (name, used, created_at) VALUES (?, 0, ?)",
            ("Alice", 1),
        )
        conn.commit()
        conn.close()

        response = app_module.app.test_client().post(
            "/api/register", json={"displayName": "Alice", "password": "secret123"}
        )

        assert response.status_code == 200
        user = fetch_user_by_display_name(db_path, "Alice")
        assert user["password_hash"].startswith("$2b$")
        assert user["salt"] == ""
        assert app_module.verify_pw("secret123", user)


def test_change_password_success_stores_bcrypt_password_without_salt(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        client = app_module.app.test_client()
        login_response = client.post(
            "/api/login", json={"username": "admin", "password": "admin123"}
        )
        token = login_response.get_json()["token"]

        response = client.put(
            "/api/change-password",
            json={"oldPassword": "admin123", "newPassword": "newsecret"},
            headers={"Authorization": f"Bearer {token}"},
        )

        assert response.status_code == 200
        admin = fetch_user(db_path, "admin")
        assert admin["password_hash"].startswith("$2b$")
        assert admin["salt"] == ""
        assert app_module.verify_pw("newsecret", admin)


def test_reset_password_success_stores_bcrypt_password_without_salt(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("bob", "Bob", app_module.hash_pw("oldpass"), "", "user", 1),
        )
        conn.commit()
        conn.close()

        client = app_module.app.test_client()
        login_response = client.post(
            "/api/login", json={"username": "admin", "password": "admin123"}
        )
        token = login_response.get_json()["token"]

        response = client.put(
            "/api/users/bob/password",
            json={"password": "newpass"},
            headers={"Authorization": f"Bearer {token}"},
        )

        assert response.status_code == 200
        user = fetch_user(db_path, "bob")
        assert user["password_hash"].startswith("$2b$")
        assert user["salt"] == ""
        assert app_module.verify_pw("newpass", user)
