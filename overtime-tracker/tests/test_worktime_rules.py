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


def test_default_worktime_is_0830_to_1730_without_lunch(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        settings = app_module.get_settings_data()

        assert settings["workStart"] == "08:30"
        assert settings["workEnd"] == "17:30"
        assert "lunchStart" not in settings
        assert "lunchEnd" not in settings


def test_backend_workday_counts_only_before_start_and_after_end(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("u1", "Alice", app_module.hash_pw("pass123"), "", "user", 1),
        )
        rows = [
            ("u1", "2026-06-09", "07:30:00", 1780951800000, "in", 31, 121, 10, 0, "early"),
            ("u1", "2026-06-09", "10:00:00", 1780960800000, "out", 31, 121, 10, 0, ""),
            ("u1", "2026-06-09", "16:00:00", 1780982400000, "in", 31, 121, 10, 0, "late"),
            ("u1", "2026-06-09", "19:00:00", 1780993200000, "out", 31, 121, 10, 0, ""),
        ]
        conn.executemany(
            """
            INSERT INTO records (
                user_id, date, time_str, ts, type, lat, lng, accuracy, out_of_range, note
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            rows,
        )
        conn.commit()
        conn.close()
        settings = {
            "workStart": "08:30",
            "workEnd": "17:30",
            "weekdays": [1, 2, 3, 4, 5],
            "holidays": [],
            "workdays": [],
        }

        assert app_module.calc_user_ot("u1", settings) == 150


def test_backend_rest_day_counts_entire_recorded_duration(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("u1", "Alice", app_module.hash_pw("pass123"), "", "user", 1),
        )
        rows = [
            ("u1", "2026-06-13", "09:00:00", 1781302800000, "in", 31, 121, 10, 0, "weekend"),
            ("u1", "2026-06-13", "12:00:00", 1781313600000, "out", 31, 121, 10, 0, ""),
        ]
        conn.executemany(
            """
            INSERT INTO records (
                user_id, date, time_str, ts, type, lat, lng, accuracy, out_of_range, note
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            rows,
        )
        conn.commit()
        conn.close()
        settings = {
            "workStart": "08:30",
            "workEnd": "17:30",
            "weekdays": [1, 2, 3, 4, 5],
            "holidays": [],
            "workdays": [],
        }

        assert app_module.calc_user_ot("u1", settings) == 180


def test_backend_cross_day_pair_counts_recorded_duration(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("u1", "Alice", app_module.hash_pw("pass123"), "", "user", 1),
        )
        rows = [
            ("u1", "2026-06-09", "23:30:00", 1781019000000, "in", 31, 121, 10, 0, "设计：图纸调整"),
            ("u1", "2026-06-10", "00:30:00", 1781022600000, "out", 31, 121, 10, 0, "实际工作持续到次日"),
        ]
        conn.executemany(
            """
            INSERT INTO records (
                user_id, date, time_str, ts, type, lat, lng, accuracy, out_of_range, note
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            rows,
        )
        conn.commit()
        conn.close()
        settings = {
            "workStart": "08:30",
            "workEnd": "17:30",
            "weekdays": [1, 2, 3, 4, 5],
            "holidays": [],
            "workdays": [],
        }

        assert app_module.calc_user_ot("u1", settings) == 60
