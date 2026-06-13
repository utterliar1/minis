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
        ("u1", "2026-06-01", "07:30:00", 1780260600000, "in", 31.24, 121.48, 42, 1, "范围外说明"),
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
        assert "范围外说明" in sent[0]["html"]
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
        assert attachment["content"].startswith("\u59d3\u540d,\u65e5\u671f,\u661f\u671f,\u4e0a\u73ed,\u4e0b\u73ed,\u7c7b\u578b,\u5de5\u4f5c\u7c7b\u522b,\u4e8b\u7531,\u4e0b\u73ed\u8bf4\u660e,\u8303\u56f4\u5916,\u5b9e\u9645\u4f4d\u7f6e,\u590d\u6838\u6807\u8bb0,\u5de5\u65f6(\u5206),\u5de5\u65f6(h)")
        assert "范围外说明" in attachment["content"]
        assert "31.240000,121.480000" in attachment["content"]


def test_email_csv_splits_work_category_clock_out_note_and_review_marker(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("u1", "Alice", app_module.hash_pw("pass123"), "", "user", 1),
        )
        rows = [
            ("u1", "2026-06-09", "23:30:00", 1781019000000, "in", 31.23, 121.47, 10, 0, "设计：图纸调整"),
            ("u1", "2026-06-10", "00:30:00", 1781022600000, "out", 31.24, 121.48, 42, 1, "跨天且结束位置变化"),
        ]
        conn.executemany(
            """
            INSERT INTO records (
                user_id, date, time_str, ts, type, lat, lng, accuracy, out_of_range, note
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            rows,
        )
        records = conn.execute(
            "SELECT r.*,u.display_name FROM records r JOIN users u ON r.user_id=u.username ORDER BY r.ts"
        ).fetchall()
        conn.close()
        settings = {
            "workStart": "08:30",
            "workEnd": "17:30",
            "lat": 31.23,
            "lng": 121.47,
            "weekdays": [1, 2, 3, 4, 5],
            "holidays": [],
            "workdays": [],
        }

        csv = app_module.build_email_export_csv([dict(r) for r in records], settings, include_person_subtotals=False)

        assert csv.startswith("姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)")
        assert '"Alice","2026-06-09","周二","23:30","00:30","工作日","设计","图纸调整","跨天且结束位置变化","是","31.240000,121.480000; 精度 42m; 距离 1463m","位置不一致；跨天",60,"1h"' in csv


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
