import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_export_csv_uses_unified_detail_and_summary_rows():
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
  lat: 31.23,
  lng: 121.47,
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};

const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 0, note: '早段' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '10:00:00', ts: 2, type: 'out', out_of_range: 0, note: '' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '16:30:00', ts: 3, type: 'in', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, note: '范围外说明' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '18:30:00', ts: 4, type: 'out', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, note: '' },
  { user_id: 'u2', display_name: 'Bob', date: '2026-06-13', time_str: '09:00:00', ts: 1781302800000, type: 'in', out_of_range: 0, note: '周末支持' },
  { user_id: 'u2', display_name: 'Bob', date: '2026-06-13', time_str: '12:00:00', ts: 1781313600000, type: 'out', out_of_range: 0, note: '' },
];

const csv = sandbox.OT.buildExportCsv(records);
const expected = [
  '姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)',
  '"Alice","2026-06-09","周二","07:30","18:30","工作日","","早段; 范围外说明","","是","31.240000,121.480000; 精度 42m; 距离 1463m","",120,"2h"',
  '"Bob","2026-06-13","周六","09:00","12:00","休息日","","周末支持","","","","",180,"3h"',
  '"汇总","","","","","总计","","","","范围外 1 天","","",300,"5h"',
].join('\n');

if (csv !== expected) {
  throw new Error(`Unexpected CSV:\n${csv}\n--- expected ---\n${expected}`);
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


def test_export_csv_splits_work_category_clock_out_note_and_review_marker():
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
  lat: 31.23,
  lng: 121.47,
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};

const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '23:30:00', ts: Date.parse('2026-06-09T23:30:00+08:00'), type: 'in', out_of_range: 0, note: '设计：图纸调整' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-10', time_str: '00:30:00', ts: Date.parse('2026-06-10T00:30:00+08:00'), type: 'out', out_of_range: 1, note: '跨天且结束位置变化' },
];

const csv = sandbox.OT.buildExportCsv(records);
const expected = [
  '姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)',
  '"Alice","2026-06-09","周二","23:30","00:30","工作日","设计","图纸调整","跨天且结束位置变化","是","","位置不一致；跨天",60,"1h"',
  '"汇总","","","","","总计","","","","范围外 1 天","","",60,"1h"',
].join('\n');

if (csv !== expected) {
  throw new Error(`Unexpected CSV:\n${csv}\n--- expected ---\n${expected}`);
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


def test_export_csv_can_insert_person_subtotals_before_final_summary():
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
  lat: 31.23,
  lng: 121.47,
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};

const records = [
  { user_id: 'u2', display_name: 'Bob', date: '2026-06-13', time_str: '09:00:00', ts: 1781302800000, type: 'in', out_of_range: 0, note: '周末支持' },
  { user_id: 'u2', display_name: 'Bob', date: '2026-06-13', time_str: '12:00:00', ts: 1781313600000, type: 'out', out_of_range: 0, note: '' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 0, note: '早段' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '10:00:00', ts: 2, type: 'out', out_of_range: 0, note: '' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '16:30:00', ts: 3, type: 'in', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, note: '范围外说明' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '18:30:00', ts: 4, type: 'out', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, note: '' },
];

const csv = sandbox.OT.buildExportCsv(records, { includePersonSubtotals: true });
const lines = csv.split('\n');
const expected = [
  '姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)',
  '"Alice","2026-06-09","周二","07:30","18:30","工作日","","早段; 范围外说明","","是","31.240000,121.480000; 精度 42m; 距离 1463m","",120,"2h"',
  '"Alice","","","","","小计","","","","范围外 1 天","","",120,"2h"',
  '"Bob","2026-06-13","周六","09:00","12:00","休息日","","周末支持","","","","",180,"3h"',
  '"Bob","","","","","小计","","","","范围外 0 天","","",180,"3h"',
  '"汇总","","","","","总计","","","","范围外 1 天","","",300,"5h"',
];

if (JSON.stringify(lines) !== JSON.stringify(expected)) {
  throw new Error(`Unexpected CSV:\n${csv}\n--- expected ---\n${expected.join('\n')}`);
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


def test_actual_location_ignores_missing_coordinates():
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
vm.runInContext(fs.readFileSync('frontend/js/utils.js', 'utf8'), sandbox, { filename: 'frontend/js/utils.js' });
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];
sandbox.OT.settings = sandbox.settings = { lat: null, lng: null };

const text = sandbox.OT.actualLocationText({ out_of_range: 1, lat: null, lng: null, accuracy: null });
if (text !== '') throw new Error(`Expected empty actual location, got ${text}`);
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


def test_actual_location_appends_address_only_when_map_key_is_configured():
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
vm.runInContext(fs.readFileSync('frontend/js/utils.js', 'utf8'), sandbox, { filename: 'frontend/js/utils.js' });
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

const record = {
  out_of_range: 1,
  lat: 31.24,
  lng: 121.48,
  accuracy: 42,
  address: '上海市黄浦区测试路',
};

sandbox.OT.settings = sandbox.settings = { lat: 31.23, lng: 121.47 };
const withoutKey = sandbox.OT.actualLocationText(record);
if (withoutKey.includes('地址')) throw new Error(`Address should not be included without map key: ${withoutKey}`);
if (withoutKey !== '31.240000,121.480000; 精度 42m; 距离 1463m') throw new Error(`Unexpected location without key: ${withoutKey}`);

sandbox.OT.settings = sandbox.settings = { lat: 31.23, lng: 121.47, mapKey: 'test-key' };
const withKey = sandbox.OT.actualLocationText(record);
if (withKey !== '31.240000,121.480000; 精度 42m; 距离 1463m; 地址 上海市黄浦区测试路') {
  throw new Error(`Unexpected location with key: ${withKey}`);
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


def test_personal_and_manager_exports_download_unified_csv():
    script = r"""
(async () => {
const fs = require('fs');
const vm = require('vm');

const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 0, note: '早段' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '10:00:00', ts: 2, type: 'out', out_of_range: 0, note: '' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '16:30:00', ts: 3, type: 'in', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, note: '范围外说明' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '18:30:00', ts: 4, type: 'out', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, note: '' },
];
const downloads = [];
const downloadPromises = [];
const sandbox = {
  Blob,
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById() { return { textContent: '', innerHTML: '', classList: { add() {}, remove() {} }, value: 'month' }; },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/records.js', 'frontend/js/admin.js']) {
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
sandbox.OT.currentUser = sandbox.currentUser = { username: 'u1', displayName: 'Alice', role: 'user' };
sandbox.OT.showToast = sandbox.showToast = function(){};
sandbox.OT.closeModalDirect = sandbox.closeModalDirect = function(){};
sandbox.OT.api = sandbox.api = async function(path) {
  if (path === '/records') return { records };
  if (path.startsWith('/export?')) return { records };
  throw new Error(`Unexpected API path ${path}`);
};
sandbox.OT.downloadBlob = sandbox.downloadBlob = function(blob, name) {
  downloadPromises.push(blob.text().then(text => downloads.push({ name, text })));
};

await sandbox.OT.exportMyRecords();
sandbox.OT.currentUser = sandbox.currentUser = { username: 'admin', displayName: 'Admin', role: 'admin' };
await sandbox.OT.exportCsv('all', 'month');
await sandbox.OT.exportCsv('u1', 'month');
await sandbox.OT.exportCsv('', 'month');
await Promise.all(downloadPromises);

if (downloads.length !== 4) throw new Error(`Expected 4 downloads, got ${downloads.length}`);
for (const item of downloads) {
  const text = item.text.replace(/^\uFEFF/, '');
  if (!text.startsWith('姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)')) throw new Error(`Missing unified header in ${item.name}: ${text}`);
  if (!text.includes('"Alice","2026-06-09","周二","07:30","18:30","工作日","","早段; 范围外说明","","是","31.240000,121.480000; 精度 42m; 距离 1463m","",120,"2h"')) throw new Error(`Missing detail row in ${item.name}: ${text}`);
  if (!text.includes('"汇总","","","","","总计","","","","范围外 1 天","","",120,"2h"')) throw new Error(`Missing summary row in ${item.name}: ${text}`);
}
if (downloads[0].text.includes('小计')) throw new Error(`Personal export should not contain subtotal: ${downloads[0].text}`);
if (!downloads[1].text.includes('"Alice","","","","","小计","","","","范围外 1 天","","",120,"2h"')) throw new Error(`Manager all export missing subtotal: ${downloads[1].text}`);
if (downloads[2].text.includes('小计')) throw new Error(`Manager single-person export should not contain subtotal: ${downloads[2].text}`);
if (!downloads[3].text.includes('"Alice","","","","","小计","","","","范围外 1 天","","",120,"2h"')) throw new Error(`Manager default export missing subtotal: ${downloads[3].text}`);
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


def test_exports_refresh_settings_before_building_actual_location():
    script = r"""
(async () => {
const fs = require('fs');
const vm = require('vm');

const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, actual_address: '上海市黄浦区测试路', note: '范围外说明' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '18:30:00', ts: 2, type: 'out', lat: 31.24, lng: 121.48, accuracy: 42, out_of_range: 1, actual_address: '上海市黄浦区测试路', note: '' },
];
const downloads = [];
const downloadPromises = [];
const sandbox = {
  Blob,
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById() { return { textContent: '', innerHTML: '', classList: { add() {}, remove() {} }, value: 'month' }; },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/stats.js', 'frontend/js/records.js', 'frontend/js/admin.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

const staleSettings = {
  workStart: '08:30',
  workEnd: '17:30',
  lat: 31.23,
  lng: 121.47,
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};
sandbox.OT.settings = sandbox.settings = staleSettings;
sandbox.OT.currentUser = sandbox.currentUser = { username: 'admin', displayName: 'Admin', role: 'admin' };
sandbox.OT.showToast = sandbox.showToast = function(){};
sandbox.OT.closeModalDirect = sandbox.closeModalDirect = function(){};
sandbox.OT.api = sandbox.api = async function(path) {
  if (path === '/settings') return { settings: { ...staleSettings, mapKey: 'test-map-key' } };
  if (path === '/records') return { records };
  if (path.startsWith('/export?')) return { records };
  throw new Error(`Unexpected API path ${path}`);
};
sandbox.OT.downloadBlob = sandbox.downloadBlob = function(blob, name) {
  downloadPromises.push(blob.text().then(text => downloads.push({ name, text })));
};

await sandbox.OT.exportCsv('all', 'month');
await Promise.all(downloadPromises);

if (downloads.length !== 1) throw new Error(`Expected 1 download, got ${downloads.length}`);
const text = downloads[0].text.replace(/^\uFEFF/, '');
if (!text.includes('地址 上海市黄浦区测试路')) {
  throw new Error(`Expected refreshed settings to include address in export: ${text}`);
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
