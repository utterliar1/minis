import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_out_of_range_records_show_actual_location_in_ui():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const elements = {};
let modalHtml = '';
const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById(id) {
      if (!elements[id]) elements[id] = { textContent: '', innerHTML: '', style: {}, classList: { add() {}, remove() {} } };
      return elements[id];
    },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/clock.js', 'frontend/js/records.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

sandbox.OT.settings = sandbox.settings = {
  workStart: '08:30',
  workEnd: '17:30',
  lat: 31.23,
  lng: 121.47,
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};
sandbox.OT.showModal = sandbox.showModal = function(html) { modalHtml = html; };

const record = {
  user_id: 'u1',
  date: '2026-06-09',
  time_str: '18:30:00',
  ts: 1780993800000,
  type: 'out',
  lat: 31.24,
  lng: 121.48,
  accuracy: 42,
  out_of_range: 1,
  note: '远程说明',
};

sandbox.OT.showLastClockResult(record, '2 小时');
const lastHtml = elements['last-clock-result'].innerHTML;
if (!lastHtml.includes('实际位置：31.240000,121.480000; 精度 42m; 距离 1463m')) {
  throw new Error(`Last result missing actual location: ${lastHtml}`);
}

sandbox.OT.allRecords = sandbox.allRecords = [record];
sandbox.OT.showDayDetail(2026, 5, 9);
if (!modalHtml.includes('实际位置：31.240000,121.480000; 精度 42m; 距离 1463m')) {
  throw new Error(`Day detail missing actual location: ${modalHtml}`);
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


def test_records_list_marks_out_of_range_days():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const elements = {};
const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById(id) {
      if (!elements[id]) elements[id] = { textContent: '', innerHTML: '', style: {}, classList: { add() {}, remove() {} } };
      return elements[id];
    },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/records.js']) {
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

const dayRecords = [
  { date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 1, note: '远程说明' },
  { date: '2026-06-09', time_str: '18:30:00', ts: 2, type: 'out', out_of_range: 1, note: '' },
];

sandbox.OT.renderRecordsList({ '2026-06-09': dayRecords });
const html = elements['records-list'].innerHTML;
if (!html.includes('class="record-flag-badge"')) {
  throw new Error(`Records list missing range marker class: ${html}`);
}
if (!html.includes('范围外')) {
  throw new Error(`Records list missing range marker text: ${html}`);
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
