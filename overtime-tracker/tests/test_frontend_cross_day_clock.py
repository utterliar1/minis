import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_cross_day_open_record_keeps_next_action_as_clock_out():
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
      style: {},
      classList: { contains(cls) { return (this.owner?.className || '').includes(cls); } },
    };
    elements[id].classList.owner = elements[id];
  }
  return elements[id];
}

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  navigator: {},
  setInterval() {},
  clearInterval() {},
  setTimeout() {},
  document: {
    getElementById: element,
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/clock.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

sandbox.OT.settings = sandbox.settings = {
  lat: 31.23,
  lng: 121.47,
  radius: 500,
  gpsAccuracy: 100,
  workStart: '08:30',
  workEnd: '17:30',
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};
sandbox.OT.currentPos = sandbox.currentPos = { lat: 31.23, lng: 121.47, accuracy: 10 };
sandbox.OT.allRecords = sandbox.allRecords = [
  { user_id: 'u1', date: '2026-06-09', time_str: '22:10:12', ts: Date.parse('2026-06-09T22:10:12+08:00'), type: 'in', note: 'night work' },
];

const realDate = Date;
class FakeDate extends realDate {
  constructor(...args) {
    if (args.length === 0) return new realDate('2026-06-10T01:00:00+08:00');
    return new realDate(...args);
  }
  static now() { return new realDate('2026-06-10T01:00:00+08:00').getTime(); }
  static parse(value) { return realDate.parse(value); }
  static UTC(...args) { return realDate.UTC(...args); }
}
sandbox.Date = FakeDate;

sandbox.OT.updateClockButton();

if (element('clock-btn-text').textContent !== '打卡下班') {
  throw new Error(`Expected cross-day next action to be clock out, got ${element('clock-btn-text').textContent}`);
}
if (!element('clock-btn').className.includes('check-out')) {
  throw new Error(`Expected check-out class, got ${element('clock-btn').className}`);
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


def test_cross_day_clock_out_pairs_with_previous_open_clock_in():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  navigator: {},
  setTimeout() {},
  document: {
    getElementById() { return { textContent: '', innerHTML: '', className: '', style: {}, classList: { contains() { return false; } } }; },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/clock.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

const clockIn = { user_id: 'u1', date: '2026-06-09', time_str: '22:10:12', ts: Date.parse('2026-06-09T22:10:12+08:00'), type: 'in' };
const clockOut = { user_id: 'u1', date: '2026-06-10', time_str: '01:05:00', ts: Date.parse('2026-06-10T01:05:00+08:00'), type: 'out' };
const found = sandbox.OT.getCurrentClockIn([clockIn, clockOut], clockOut);

if (found !== clockIn) throw new Error('Expected cross-day clock out to pair with previous open clock in');
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


def test_clock_out_note_context_distinguishes_cross_day_and_location_mismatch():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  navigator: {},
  setTimeout() {},
  document: {
    getElementById() { return { textContent: '', innerHTML: '', className: '', style: {}, classList: { contains() { return false; } } }; },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/clock.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

const clockIn = { user_id: 'u1', date: '2026-06-09', time_str: '23:30:00', ts: Date.parse('2026-06-09T23:30:00+08:00'), type: 'in', out_of_range: 0 };
let ctx = sandbox.OT.clockOutReviewContext(clockIn, true, '2026-06-09');
if (!ctx.required) throw new Error('Expected location mismatch to require note');
if (ctx.flags.join('；') !== '位置不一致') throw new Error(`Bad mismatch flags ${ctx.flags}`);
if (!ctx.options.includes('临时外出后结束记录')) throw new Error(`Missing location option ${ctx.options}`);

ctx = sandbox.OT.clockOutReviewContext(clockIn, false, '2026-06-10');
if (!ctx.required) throw new Error('Expected cross-day to require note');
if (ctx.flags.join('；') !== '跨天') throw new Error(`Bad cross-day flags ${ctx.flags}`);
if (!ctx.options.includes('实际工作持续到次日')) throw new Error(`Missing actual continued option ${ctx.options}`);
if (!ctx.options.includes('忘记下班打卡，当前补记')) throw new Error(`Missing forgot option ${ctx.options}`);
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
