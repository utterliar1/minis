import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def run_node(script):
    return subprocess.run(
        ["node", "-e", script],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )


def test_bulk_whitelist_parser_prefers_lines_and_common_punctuation():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: { getElementById() { return null; } },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/admin.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}

const names = sandbox.OT.parseBulkWhitelistNames('张三\n李四、王五,赵六;钱七；孙八\n欧阳 娜娜');
const expected = ['张三', '李四', '王五', '赵六', '钱七', '孙八', '欧阳 娜娜'];
if (JSON.stringify(names) !== JSON.stringify(expected)) {
  throw new Error(`unexpected names: ${JSON.stringify(names)}`);
}
"""
    result = run_node(script)
    assert result.returncode == 0, result.stderr


def test_bulk_whitelist_submit_calls_api_and_reports_summary():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const elements = {};
function element(id) {
  if (!elements[id]) {
    elements[id] = {
      id,
      value: '',
      textContent: '',
      innerHTML: '',
      className: '',
      style: {},
      classList: { add() {}, remove() {} },
    };
  }
  return elements[id];
}

const requests = [];
const toasts = [];
let reloaded = false;
let closed = false;

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
for (const file of ['frontend/js/utils.js', 'frontend/js/admin.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}

sandbox.OT.api = sandbox.api = async function(path, opts) {
  requests.push({ path, body: JSON.parse(opts.body), method: opts.method });
  return { counts: { added: 2, skipped: 1, invalid: 0 } };
};
sandbox.OT.showToast = sandbox.showToast = msg => toasts.push(msg);
sandbox.OT.closeModalDirect = sandbox.closeModalDirect = () => { closed = true; };
sandbox.OT.loadWhitelist = sandbox.loadWhitelist = async () => { reloaded = true; };

element('wl-bulk-input').value = '张三\n李四、张三';

(async () => {
  await sandbox.OT.submitBulkWhitelist();
  if (requests.length !== 1) throw new Error('expected one API request');
  if (requests[0].path !== '/whitelist/bulk') throw new Error(`bad path ${requests[0].path}`);
  if (requests[0].method !== 'POST') throw new Error(`bad method ${requests[0].method}`);
  const expectedNames = ['张三', '李四', '张三'];
  if (JSON.stringify(requests[0].body.names) !== JSON.stringify(expectedNames)) {
    throw new Error(`bad request names: ${JSON.stringify(requests[0].body.names)}`);
  }
  if (!closed) throw new Error('modal should close');
  if (!reloaded) throw new Error('whitelist should reload');
  if (!toasts[0].includes('已添加 2 人') || !toasts[0].includes('跳过 1 人')) {
    throw new Error(`bad toast ${toasts[0]}`);
  }
})().catch(err => {
  console.error(err);
  process.exit(1);
});
"""
    result = run_node(script)
    assert result.returncode == 0, result.stderr
