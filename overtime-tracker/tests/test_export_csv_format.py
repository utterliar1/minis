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
  weekdays: [1, 2, 3, 4, 5],
  holidays: [],
  workdays: [],
};

const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 0, note: '早段' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '10:00:00', ts: 2, type: 'out', out_of_range: 0, note: '' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '16:30:00', ts: 3, type: 'in', out_of_range: 1, note: '远程说明' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '18:30:00', ts: 4, type: 'out', out_of_range: 1, note: '' },
  { user_id: 'u2', display_name: 'Bob', date: '2026-06-13', time_str: '09:00:00', ts: 1781302800000, type: 'in', out_of_range: 0, note: '周末支持' },
  { user_id: 'u2', display_name: 'Bob', date: '2026-06-13', time_str: '12:00:00', ts: 1781313600000, type: 'out', out_of_range: 0, note: '' },
];

const csv = sandbox.OT.buildExportCsv(records);
const expected = [
  '姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)',
  '"Alice","2026-06-09","周二","07:30","18:30","工作日","早段; 远程说明","是",120,"2h"',
  '"Bob","2026-06-13","周六","09:00","12:00","休息日","周末支持","",180,"3h"',
  '"汇总","","","","","","","远程天数 1",300,"5h"',
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


def test_personal_and_manager_exports_download_unified_csv():
    script = r"""
(async () => {
const fs = require('fs');
const vm = require('vm');

const records = [
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '07:30:00', ts: 1, type: 'in', out_of_range: 0, note: '早段' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '10:00:00', ts: 2, type: 'out', out_of_range: 0, note: '' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '16:30:00', ts: 3, type: 'in', out_of_range: 1, note: '远程说明' },
  { user_id: 'u1', display_name: 'Alice', date: '2026-06-09', time_str: '18:30:00', ts: 4, type: 'out', out_of_range: 1, note: '' },
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
await Promise.all(downloadPromises);

if (downloads.length !== 2) throw new Error(`Expected 2 downloads, got ${downloads.length}`);
for (const item of downloads) {
  const text = item.text.replace(/^\uFEFF/, '');
  if (!text.startsWith('姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)')) throw new Error(`Missing unified header in ${item.name}: ${text}`);
  if (!text.includes('"Alice","2026-06-09","周二","07:30","18:30","工作日","早段; 远程说明","是",120,"2h"')) throw new Error(`Missing detail row in ${item.name}: ${text}`);
  if (!text.includes('"汇总","","","","","","","远程天数 1",120,"2h"')) throw new Error(`Missing summary row in ${item.name}: ${text}`);
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
