import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_frontend_workday_counts_only_before_start_and_after_end():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById() { return { textContent: '', innerHTML: '', classList: { add() {}, remove() {} } }; },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
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

const records = [
  { date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in' },
  { date: '2026-06-09', time_str: '10:00:00', ts: 2, type: 'out' },
  { date: '2026-06-09', time_str: '16:00:00', ts: 3, type: 'in' },
  { date: '2026-06-09', time_str: '19:00:00', ts: 4, type: 'out' },
];

const minutes = sandbox.OT.calcTodayOT(records, new Date('2026-06-09T12:00:00'));
if (minutes !== 150) throw new Error(`Expected 150, got ${minutes}`);
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


def test_frontend_cross_day_group_counts_recorded_duration():
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

const records = [
  { user_id: 'u1', date: '2026-06-09', time_str: '23:30:00', ts: Date.parse('2026-06-09T23:30:00+08:00'), type: 'in' },
  { user_id: 'u1', date: '2026-06-10', time_str: '00:30:00', ts: Date.parse('2026-06-10T00:30:00+08:00'), type: 'out' },
];
const groups = sandbox.OT.groupRecordsByStartDate(records);
const minutes = sandbox.OT.calcTodayOT(groups['2026-06-09'], new Date('2026-06-09T12:00:00'));
if (minutes !== 60) throw new Error(`Expected cross-day group to count 60 minutes, got ${minutes}`);
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
