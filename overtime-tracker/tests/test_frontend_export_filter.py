import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_admin_dashboard_preserves_export_member_when_period_changes():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const elements = {};
function element(id) {
  if (!elements[id]) {
    const el = { id, _innerHTML: '', value: '', textContent: '', style: {}, classList: { add() {}, remove() {} } };
    Object.defineProperty(el, 'innerHTML', {
      get() { return this._innerHTML; },
      set(html) {
        this._innerHTML = html;
        if (id === 'export-scope') {
          const values = [...String(html).matchAll(/<option value="([^"]*)"/g)].map(m => m[1]);
          this.value = values[0] || '';
        }
      },
    });
    elements[id] = el;
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
sandbox.OT.api = sandbox.api = async function(path) {
  if (path === '/records/all') return { records: [] };
  if (path === '/users') return { users: [
    { username: 'admin', display_name: 'Admin' },
    { username: 'u1', display_name: 'Alice' },
    { username: 'u2', display_name: 'Bob' },
  ] };
  throw new Error(`Unexpected API path ${path}`);
};

element('export-period').value = 'week';
element('export-scope').innerHTML = '<option value="all">All</option><option value="u1">Alice</option><option value="u2">Bob</option>';
element('export-scope').value = 'u1';

(async () => {
  await sandbox.OT.loadDashboard();
  if (element('export-scope').value !== 'u1') {
    throw new Error(`export member reset to ${element('export-scope').value}`);
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
