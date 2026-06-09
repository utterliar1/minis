import importlib
import sys
from contextlib import contextmanager
from datetime import datetime
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


def test_resolve_report_period_ranges(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        now = datetime.fromisoformat("2026-06-09T15:00:00")

        assert app_module.resolve_report_period("yesterday", now) == ("2026-06-08", "2026-06-08")
        assert app_module.resolve_report_period("this_week", now) == ("2026-06-08", "2026-06-09")
        assert app_module.resolve_report_period("last_week", now) == ("2026-06-01", "2026-06-07")
        assert app_module.resolve_report_period("this_month", now) == ("2026-06-01", "2026-06-09")
        assert app_module.resolve_report_period("last_month", now) == ("2026-05-01", "2026-05-31")


def test_next_schedule_time_supports_daily_weekly_monthly_and_month_end(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as app_module:
        now = datetime.fromisoformat("2026-06-09T15:00:00")

        daily = app_module.next_schedule_time(
            {"schedule_frequency": "daily", "schedule_hour": 9, "schedule_minute": 30}, now
        )
        weekly = app_module.next_schedule_time(
            {"schedule_frequency": "weekly", "schedule_weekday": 5, "schedule_hour": 9, "schedule_minute": 30}, now
        )
        monthly = app_module.next_schedule_time(
            {"schedule_frequency": "monthly", "schedule_month_day": "last", "schedule_hour": 9, "schedule_minute": 0}, now
        )
        monthly_next = app_module.next_schedule_time(
            {"schedule_frequency": "monthly", "schedule_month_day": "8", "schedule_hour": 9, "schedule_minute": 0}, now
        )

        assert daily.isoformat() == "2026-06-10T09:30:00"
        assert weekly.isoformat() == "2026-06-12T09:30:00"
        assert monthly.isoformat() == "2026-06-30T09:00:00"
        assert monthly_next.isoformat() == "2026-07-08T09:00:00"
