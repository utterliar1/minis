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


def remote_clock_payload(clock_type, note, out_of_range=True):
    return {
        "type": clock_type,
        "lat": 31.24,
        "lng": 121.48,
        "accuracy": 10,
        "outOfRange": out_of_range,
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


def test_clock_out_allows_empty_note(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)

        response = client.post(
            "/api/clock",
            json=clock_payload("in", "项目推进"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200

        response = client.post(
            "/api/clock",
            json=clock_payload("out", ""),
            headers=auth_headers(member_token),
        )

        assert response.status_code == 200
        assert response.get_json()["type"] == "out"


def test_invalid_location_takes_precedence_over_empty_note(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)
        payload = clock_payload("in", "")
        payload["lat"] = "invalid"
        payload["lng"] = None

        response = client.post(
            "/api/clock",
            json=payload,
            headers=auth_headers(member_token),
        )
        error = response.get_json()["error"]

        assert response.status_code == 400
        assert "定位" in error
        assert "事由" not in error


def test_clock_sequence_takes_precedence_over_empty_note(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)

        response = client.post(
            "/api/clock",
            json=clock_payload("in", "项目推进"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200

        response = client.post(
            "/api/clock",
            json=clock_payload("in", ""),
            headers=auth_headers(member_token),
        )
        error = response.get_json()["error"]

        assert response.status_code == 400
        assert "顺序" in error
        assert "事由" not in error


def test_cross_day_clock_out_requires_explanation_and_keeps_out_sequence(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)
        monkeypatch.setattr(app_module.time, "time", lambda: 1781019000.0)  # 2026-06-09 23:30 +08

        response = client.post(
            "/api/clock",
            json=clock_payload("in", "设计：图纸调整"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200

        monkeypatch.setattr(app_module.time, "time", lambda: 1781022600.0)  # 2026-06-10 00:30 +08
        response = client.post(
            "/api/clock",
            json=clock_payload("out", ""),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 400
        assert "下班说明" in response.get_json()["error"]

        response = client.post(
            "/api/clock",
            json=clock_payload("out", "实际工作持续到次日"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200
        assert response.get_json()["type"] == "out"


def test_clock_out_location_mismatch_requires_explanation(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        configure_location(client, admin_token)
        member_token = create_member(client, admin_token)

        response = client.post(
            "/api/clock",
            json=clock_payload("in", "销售：客户沟通"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200

        response = client.post(
            "/api/clock",
            json=remote_clock_payload("out", ""),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 400
        assert "下班说明" in response.get_json()["error"]

        response = client.post(
            "/api/clock",
            json=remote_clock_payload("out", "临时外出后结束记录"),
            headers=auth_headers(member_token),
        )
        assert response.status_code == 200
        assert response.get_json()["type"] == "out"
