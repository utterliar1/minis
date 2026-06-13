import importlib.util
import sys
from contextlib import contextmanager
from pathlib import Path


BACKEND_APP = Path(__file__).resolve().parents[1] / "backend" / "app.py"


@contextmanager
def load_app(monkeypatch, tmp_path):
    db_path = tmp_path / "overtime.db"
    monkeypatch.setenv("DB_PATH", str(db_path))
    monkeypatch.delenv("JWT_SECRET", raising=False)

    module_name = f"_overtime_app_for_export_test_{id(db_path)}"
    old_module = sys.modules.get(module_name)
    spec = importlib.util.spec_from_file_location(module_name, BACKEND_APP)
    module = importlib.util.module_from_spec(spec)

    try:
        spec.loader.exec_module(module)
        module.app.config["TESTING"] = True
        yield module
    finally:
        if old_module is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = old_module


def auth_headers(token):
    return {"Authorization": f"Bearer {token}"}


def test_export_records_include_remote_marker_and_note(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("u1", "张三", app_module.hash_pw("pass123"), "", "user", 1),
        )
        conn.execute(
            """
            INSERT INTO records (
                user_id, date, time_str, ts, type, lat, lng, accuracy, out_of_range, note
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            ("u1", "2026-06-08", "09:00", 1780870800000, "in", None, None, None, 1, "范围外说明"),
        )
        conn.commit()
        conn.close()

        client = app_module.app.test_client()
        login_response = client.post(
            "/api/login",
            json={"username": "张三", "password": "pass123"},
        )
        assert login_response.status_code == 200
        token = login_response.get_json()["token"]

        response = client.get("/api/export?period=all", headers=auth_headers(token))

        assert response.status_code == 200
        record = response.get_json()["records"][0]
        assert record["out_of_range"] == 1
        assert record["note"] == "范围外说明"
