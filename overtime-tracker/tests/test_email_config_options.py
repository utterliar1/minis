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


def test_email_config_defaults_include_report_options(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        token = login(client, "admin", "admin123")

        response = client.get("/api/email-config", headers=auth_headers(token))

        assert response.status_code == 200
        config = response.get_json()["config"]
        assert config["schedule_frequency"] == "daily"
        assert config["schedule_weekday"] == 1
        assert config["schedule_month_day"] == "last"
        assert config["report_period"] == "this_month"
        assert config["report_content"] == "summary"
        assert config["member_filter"] == "all"
        assert config["include_out_of_range"] == 1


def test_email_config_put_preserves_masked_password_and_saves_options(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        client = app_module.app.test_client()
        token = login(client, "admin", "admin123")

        first = client.put(
            "/api/email-config",
            headers=auth_headers(token),
            json={
                "smtp_host": "smtp.qq.com",
                "smtp_port": 465,
                "smtp_user": "sender@example.com",
                "smtp_pass": "secret-pass",
                "sender_name": "????",
                "recipients": ["boss@example.com"],
                "schedule_frequency": "weekly",
                "schedule_weekday": 5,
                "schedule_month_day": "last",
                "report_period": "last_week",
                "report_content": "summary_csv",
                "member_filter": "with_records",
                "include_out_of_range": 1,
            },
        )
        assert first.status_code == 200

        second = client.put(
            "/api/email-config",
            headers=auth_headers(token),
            json={
                "smtp_pass": "\u2022\u2022\u2022\u2022\u2022\u2022",
                "schedule_frequency": "monthly",
                "schedule_month_day": "15",
            },
        )
        assert second.status_code == 200

        response = client.get("/api/email-config", headers=auth_headers(token))
        config = response.get_json()["config"]
        assert config["smtp_pass"] == "•" * 6
        assert config["schedule_frequency"] == "monthly"
        assert config["schedule_month_day"] == "15"
        assert config["report_period"] == "last_week"
        assert config["report_content"] == "summary_csv"
        assert config["member_filter"] == "with_records"
        assert config["include_out_of_range"] == 1
