import hashlib
import importlib
import sqlite3
import sys


BACKEND_DIR = "D:\\Documents\\GitHub\\minis\\overtime-tracker\\backend"


def load_app(monkeypatch, tmp_path):
    db_path = tmp_path / "overtime.db"
    monkeypatch.setenv("DB_PATH", str(db_path))
    monkeypatch.delenv("JWT_SECRET", raising=False)
    sys.modules.pop("app", None)
    if BACKEND_DIR not in sys.path:
        sys.path.insert(0, BACKEND_DIR)

    module = importlib.import_module("app")
    module.app.config["TESTING"] = True
    return module, db_path


def fetch_user(db_path, username):
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    row = conn.execute("SELECT * FROM users WHERE username=?", (username,)).fetchone()
    conn.close()
    return row


def test_default_admin_password_uses_bcrypt(monkeypatch, tmp_path):
    app_module, db_path = load_app(monkeypatch, tmp_path)

    admin = fetch_user(db_path, "admin")

    assert admin["password_hash"].startswith("$2b$")
    assert admin["salt"] == ""
    assert app_module.verify_pw("admin123", admin)


def test_legacy_sha256_user_migrates_to_bcrypt_after_login(monkeypatch, tmp_path):
    app_module, db_path = load_app(monkeypatch, tmp_path)
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
    first_app, db_path = load_app(monkeypatch, tmp_path)
    secret_file = db_path.parent / "jwt_secret"

    assert secret_file.exists()
    first_secret = first_app.SECRET
    assert secret_file.read_text(encoding="utf-8") == first_secret

    second_app, _ = load_app(monkeypatch, tmp_path)

    assert second_app.SECRET == first_secret
    assert secret_file.read_text(encoding="utf-8") == first_secret
