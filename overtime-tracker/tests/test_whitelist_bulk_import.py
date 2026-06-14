import importlib
import sqlite3
import sys
from contextlib import contextmanager
from pathlib import Path


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


def auth_headers(token):
    return {"Authorization": f"Bearer {token}"}


def login(client, username, password):
    response = client.post("/api/login", json={"username": username, "password": password})
    assert response.status_code == 200
    return response.get_json()["token"]


def whitelist_names(db_path):
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    rows = conn.execute("SELECT name, used FROM whitelist ORDER BY name").fetchall()
    conn.close()
    return [(row["name"], row["used"]) for row in rows]


def add_single_whitelist(client, token, name):
    response = client.post(
        "/api/whitelist",
        json={"name": name},
        headers=auth_headers(token),
    )
    assert response.status_code == 200


def create_member(client, admin_token, display_name):
    add_single_whitelist(client, admin_token, display_name)
    response = client.post(
        "/api/register",
        json={"displayName": display_name, "password": "secret123"},
    )
    assert response.status_code == 200
    return login(client, display_name, "secret123")


def test_bulk_whitelist_adds_unique_names_and_reports_skips(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        add_single_whitelist(client, admin_token, "王五")

        response = client.post(
            "/api/whitelist/bulk",
            json={"names": [" 张三 ", "李四", "张三", "王五", "   "]},
            headers=auth_headers(admin_token),
        )

        assert response.status_code == 200
        body = response.get_json()
        assert body["added"] == ["张三", "李四"]
        assert body["skippedExisting"] == ["王五"]
        assert body["skippedDuplicate"] == ["张三"]
        assert body["invalid"] == [""]
        assert body["counts"] == {"added": 2, "skipped": 2, "invalid": 1}
        assert whitelist_names(db_path) == [("张三", 0), ("李四", 0), ("王五", 0)]


def test_bulk_whitelist_requires_admin(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _db_path):
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        member_token = create_member(client, admin_token, "普通成员")

        unauth = client.post("/api/whitelist/bulk", json={"names": ["张三"]})
        forbidden = client.post(
            "/api/whitelist/bulk",
            json={"names": ["李四"]},
            headers=auth_headers(member_token),
        )

        assert unauth.status_code == 401
        assert forbidden.status_code == 403


def test_bulk_whitelist_rejects_empty_name_list(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _db_path):
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")

        response = client.post(
            "/api/whitelist/bulk",
            json={"names": []},
            headers=auth_headers(admin_token),
        )

        assert response.status_code == 400
        assert response.get_json()["error"] == "请输入成员姓名"
