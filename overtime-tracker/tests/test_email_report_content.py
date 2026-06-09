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


def seed_email_report_data(app_module, db_path, report_content="summary", member_filter="with_records"):
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    conn.execute(
        """
        UPDATE email_config SET smtp_host=?, smtp_user=?, smtp_pass=?, recipients=?,
            report_period=?, report_content=?, member_filter=?, include_out_of_range=?
        WHERE id=1
        """,
        (
            "smtp.example.com",
            "sender@example.com",
            "secret",
            '["boss@example.com"]',
            "last_week",
            report_content,
            member_filter,
            1,
        ),
    )
    for username, name in [("u1", "Alice"), ("u2", "No Records")]:
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            (username, name, app_module.hash_pw("pass123"), "", "user", 1),
        )
    rows = [
        ("u1", "2026-06-01", "07:30:00", 1780260600000, "in", 31.24, 121.48, 42, 1, "远程说明"),
        ("u1", "2026-06-01", "18:30:00", 1780300200000, "out", 31.24, 121.48, 42, 1, ""),
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


def test_send_overtime_report_builds_summary_for_selected_period(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        seed_email_report_data(app_module, db_path)
        monkeypatch.setattr(app_module, "bj_now", lambda: app_module.datetime.fromisoformat("2026-06-09T15:00:00+08:00"))
        sent = []

        def fake_send_email(to, subject, html_body, attachments=None):
            sent.append({"to": to, "subject": subject, "html": html_body, "attachments": attachments or []})
            return True, "ok"

        monkeypatch.setattr(app_module, "send_email", fake_send_email)

        ok, msg = app_module.send_overtime_report()

        assert ok is True
        assert msg == "\u5df2\u53d1\u9001\u7ed9 1 \u4f4d\u6536\u4ef6\u4eba"
        assert sent[0]["to"] == "boss@example.com"
        assert "2026-06-01 \u81f3 2026-06-07" in sent[0]["subject"]
        assert "Alice" in sent[0]["html"]
        assert "No Records" not in sent[0]["html"]
        assert "\u8303\u56f4\u5916\u8bb0\u5f55" in sent[0]["html"]
        assert "\u8fdc\u7a0b\u8bf4\u660e" in sent[0]["html"]
        assert "31.240000,121.480000" in sent[0]["html"]
        assert sent[0]["attachments"] == []


def test_send_overtime_report_supports_csv_only_payload(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        seed_email_report_data(app_module, db_path, report_content="csv")
        monkeypatch.setattr(app_module, "bj_now", lambda: app_module.datetime.fromisoformat("2026-06-09T15:00:00+08:00"))
        sent = []
        monkeypatch.setattr(
            app_module,
            "send_email",
            lambda to, subject, html_body, attachments=None: sent.append(
                {"subject": subject, "html": html_body, "attachments": attachments or []}
            ) or (True, "ok"),
        )

        ok, _ = app_module.send_overtime_report()

        assert ok is True
        assert "CSV \u9644\u4ef6" in sent[0]["html"]
        assert len(sent[0]["attachments"]) == 1
        attachment = sent[0]["attachments"][0]
        assert attachment["filename"].endswith(".csv")
        assert attachment["content"].startswith("\u59d3\u540d,\u65e5\u671f,\u661f\u671f,\u4e0a\u73ed,\u4e0b\u73ed,\u7c7b\u578b,\u4e8b\u7531,\u8fdc\u7a0b,\u5b9e\u9645\u4f4d\u7f6e,\u5de5\u65f6(\u5206),\u5de5\u65f6(h)")
        assert "\u8fdc\u7a0b\u8bf4\u660e" in attachment["content"]
        assert "31.240000,121.480000" in attachment["content"]


def test_send_overtime_report_supports_summary_and_csv_payload(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        seed_email_report_data(app_module, db_path, report_content="summary_csv", member_filter="all")
        monkeypatch.setattr(app_module, "bj_now", lambda: app_module.datetime.fromisoformat("2026-06-09T15:00:00+08:00"))
        sent = []
        monkeypatch.setattr(
            app_module,
            "send_email",
            lambda to, subject, html_body, attachments=None: sent.append(
                {"html": html_body, "attachments": attachments or []}
            ) or (True, "ok"),
        )

        ok, _ = app_module.send_overtime_report()

        assert ok is True
        assert "Alice" in sent[0]["html"]
        assert "No Records" in sent[0]["html"]
        assert len(sent[0]["attachments"]) == 1
