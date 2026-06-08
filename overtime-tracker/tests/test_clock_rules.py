import importlib
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
        yield module
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


def configure_location(client, admin_token):
    response = client.put(
        "/api/settings",
        json={"lat": 31.23, "lng": 121.47, "radius": 500, "gpsAccuracy": 100},
        headers=auth_headers(admin_token),
    )
    assert response.status_code == 200


def create_member(client, admin_token, display_name="Alice"):
    response = client.post(
        "/api/whitelist",
        json={"name": display_name},
        headers=auth_headers(admin_token),
    )
    assert response.status_code == 200

    password = "secret123"
    response = client.post(
        "/api/register",
        json={"displayName": display_name, "password": password},
    )
    assert response.status_code == 200

    return login(client, display_name, password)


def clock_payload(clock_type, note):
    return {
        "type": clock_type,
        "lat": 31.23,
        "lng": 121.47,
        "accuracy": 10,
        "note": note,
    }


def test_clock_in_requires_note(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)

        response = client.post(
            "/api/clock",
            json=clock_payload("in", "   "),
            headers=auth_headers(member_token),
        )

        assert response.status_code == 400
        assert "事由" in response.get_json()["error"]


def test_clock_out_requires_note(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)

        response = client.post(
            "/api/clock",
            json=clock_payload("in", "项目加班"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200

        response = client.post(
            "/api/clock",
            json=clock_payload("out", ""),
            headers=auth_headers(member_token),
        )

        assert response.status_code == 400
        assert "事由" in response.get_json()["error"]
