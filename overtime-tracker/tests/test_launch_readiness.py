import importlib
import sqlite3
import subprocess
import sys
from contextlib import contextmanager
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BACKEND_DIR = ROOT / "backend"
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


def login_token(client, username="admin", password="admin123"):
    return client.post("/api/login", json={"username": username, "password": password}).get_json()["token"]


def test_records_are_not_deleted_by_admin_cleanup_endpoints(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        conn = app_module.get_db()
        conn.execute(
            "INSERT INTO users VALUES (?,?,?,?,?,?)",
            ("u1", "Alice", app_module.hash_pw("pass123"), "", "user", 1),
        )
        conn.execute(
            "INSERT INTO records (user_id,date,time_str,ts,type,lat,lng,accuracy,out_of_range,note) VALUES (?,?,?,?,?,?,?,?,?,?)",
            ("u1", "2026-06-09", "08:00:00", 1, "in", 31.23, 121.47, 10, 0, "设计：图纸调整"),
        )
        conn.commit()
        conn.close()

        client = app_module.app.test_client()
        token = login_token(client)
        headers = {"Authorization": f"Bearer {token}"}

        delete_all = client.delete("/api/records/all", headers=headers)
        delete_user = client.delete("/api/users/u1", headers=headers)

        conn = sqlite3.connect(db_path)
        record_count = conn.execute("SELECT COUNT(*) FROM records WHERE user_id='u1'").fetchone()[0]
        user_count = conn.execute("SELECT COUNT(*) FROM users WHERE username='u1'").fetchone()[0]
        conn.close()

        assert delete_all.status_code in (404, 405)
        assert delete_user.status_code == 400
        assert record_count == 1
        assert user_count == 1


def test_email_summary_counts_cross_day_records_by_start_date(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        settings = {
            "workStart": "08:30",
            "workEnd": "17:30",
            "weekdays": [1, 2, 3, 4, 5],
            "holidays": [],
            "workdays": [],
            "lat": 31.23,
            "lng": 121.47,
        }
        records = [
            {
                "user_id": "u1",
                "display_name": "Alice",
                "date": "2026-06-09",
                "time_str": "23:30:00",
                "ts": 1781019000000,
                "type": "in",
                "out_of_range": 0,
                "note": "设计：图纸调整",
            },
            {
                "user_id": "u1",
                "display_name": "Alice",
                "date": "2026-06-10",
                "time_str": "00:30:00",
                "ts": 1781022600000,
                "type": "out",
                "out_of_range": 1,
                "note": "跨天且结束位置变化",
                "lat": 31.24,
                "lng": 121.48,
                "accuracy": 42,
            },
        ]
        users = [{"username": "u1", "display_name": "Alice"}]

        minutes = app_module.calc_records_minutes(records, settings)
        html = app_module.build_email_summary_html(
            users,
            records,
            settings,
            {"include_out_of_range": 1},
            "2026-06-09",
            "2026-06-10",
            app_module.datetime.fromisoformat("2026-06-10T09:00:00+08:00"),
        )

        assert minutes == 60
        assert "1小时" in html


def test_frontend_summary_widgets_use_start_date_grouping_for_cross_day_records():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const elements = {};
function element(id) {
  if (!elements[id]) {
    elements[id] = {
      id,
      textContent: '',
      innerHTML: '',
      className: '',
      value: 'month',
      style: {},
      classList: { add() {}, remove() {}, contains() { return false; } },
    };
  }
  return elements[id];
}

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById: element,
    createElement() { return { click() {} }; },
    querySelectorAll() { return []; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/clock.js', 'frontend/js/admin.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

sandbox.OT.settings = sandbox.settings = {
  workStart: '08:30',
  workEnd: '17:30',
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};
sandbox.OT.currentUser = sandbox.currentUser = { username: 'u1', displayName: 'Alice', role: 'user' };
sandbox.OT.allRecords = sandbox.allRecords = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '23:30:00', ts: Date.parse('2026-06-09T23:30:00+08:00'), type: 'in' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-10', time_str: '00:30:00', ts: Date.parse('2026-06-10T00:30:00+08:00'), type: 'out', note: '跨天且结束位置变化' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-05-01', time_str: '20:00:00', ts: Date.parse('2026-05-01T20:00:00+08:00'), type: 'in' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-05-01', time_str: '21:00:00', ts: Date.parse('2026-05-01T21:00:00+08:00'), type: 'out' },
];

const realDate = Date;
class FakeDate extends realDate {
  constructor(...args) {
    if (args.length === 0) return new realDate('2026-06-10T09:00:00+08:00');
    return new realDate(...args);
  }
  static now() { return new realDate('2026-06-10T09:00:00+08:00').getTime(); }
  static parse(value) { return realDate.parse(value); }
  static UTC(...args) { return realDate.UTC(...args); }
}
sandbox.Date = FakeDate;

sandbox.OT.updateTodayTimeline();
if (!elements['today-card'].style.display || elements['today-card'].style.display === 'none') {
  throw new Error('Expected today card to stay visible for a cross-day pair');
}
if (!elements['today-overtime'].textContent.includes('1 小时')) {
  throw new Error(`Expected today card to show 1 hour, got ${elements['today-overtime'].textContent}`);
}

sandbox.OT.renderStats();
if (!elements['stats-content'].innerHTML.includes('本月工时</span><span class="value overtime">1 小时')) {
  throw new Error(`Expected this-month stats to exclude May records and show 1 hour: ${elements['stats-content'].innerHTML}`);
}

sandbox.OT.updateHeaderStats();
if (!elements['header-stats'].innerHTML.includes('<div class="stat-value">1h</div><div class="stat-label">本月</div>')) {
  throw new Error(`Expected header month stats to show 1h: ${elements['header-stats'].innerHTML}`);
}
"""
    result = subprocess.run(
        ["node", "-e", script],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )

    assert result.returncode == 0, result.stderr


def test_admin_dashboard_uses_start_date_grouping_for_cross_day_records():
    script = r"""
(async () => {
const fs = require('fs');
const vm = require('vm');

const elements = {};
function element(id) {
  if (!elements[id]) {
    elements[id] = {
      id,
      textContent: '',
      innerHTML: '',
      className: '',
      value: 'month',
      style: {},
      classList: { add() {}, remove() {}, contains() { return false; } },
    };
  }
  return elements[id];
}

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById: element,
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/admin.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

sandbox.OT.settings = sandbox.settings = {
  workStart: '08:30',
  workEnd: '17:30',
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};
sandbox.OT.currentUser = sandbox.currentUser = { username: 'admin', displayName: 'Admin', role: 'admin' };
const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '23:30:00', ts: Date.parse('2026-06-09T23:30:00+08:00'), type: 'in' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-10', time_str: '00:30:00', ts: Date.parse('2026-06-10T00:30:00+08:00'), type: 'out', note: '跨天且结束位置变化' },
];
const users = [{ username: 'u1', display_name: 'Alice', role: 'user' }];
sandbox.OT.api = sandbox.api = async function(path) {
  if (path === '/records/all') return { records };
  if (path === '/users') return { users };
  throw new Error(`Unexpected API path ${path}`);
};

const realDate = Date;
class FakeDate extends realDate {
  constructor(...args) {
    if (args.length === 0) return new realDate('2026-06-10T09:00:00+08:00');
    return new realDate(...args);
  }
  static now() { return new realDate('2026-06-10T09:00:00+08:00').getTime(); }
  static parse(value) { return realDate.parse(value); }
  static UTC(...args) { return realDate.UTC(...args); }
}
sandbox.Date = FakeDate;

await sandbox.OT.loadDashboard();

if (!elements['dash-content'].innerHTML.includes('<td class="ot-positive">1h</td>')) {
  throw new Error(`Expected dashboard row to show 1h today: ${elements['dash-content'].innerHTML}`);
}
if (!elements['header-stats'].innerHTML.includes('<div class="stat-value">1h</div><div class="stat-label">今日团队工时</div>')) {
  throw new Error(`Expected admin header today total to show 1h: ${elements['header-stats'].innerHTML}`);
}
})().catch(err => {
  console.error(err);
  process.exit(1);
});
"""
    result = subprocess.run(
        ["node", "-e", script],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )

    assert result.returncode == 0, result.stderr


def test_export_csv_uses_range_outside_wording_instead_of_remote():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];
sandbox.OT.settings = sandbox.settings = {
  workStart: '08:30',
  workEnd: '17:30',
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};

const csv = sandbox.OT.buildExportCsv([
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 1, note: '设计：现场沟通' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '08:30:00', ts: 2, type: 'out', out_of_range: 1, note: '' },
]);

if (!csv.startsWith('姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)')) {
  throw new Error(`Expected header to use 范围外, got ${csv.split('\n')[0]}`);
}
if (csv.includes('远程')) {
  throw new Error(`Expected CSV not to use 远程 wording: ${csv}`);
}
if (!csv.includes('范围外 1 天')) {
  throw new Error(`Expected summary to use 范围外 N 天: ${csv}`);
}
"""
    result = subprocess.run(
        ["node", "-e", script],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )

    assert result.returncode == 0, result.stderr
