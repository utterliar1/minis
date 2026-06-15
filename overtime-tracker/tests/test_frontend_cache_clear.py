import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_clear_app_cache_keeps_login_and_refreshes_static_cache():
    script = r"""
(async () => {
const fs = require('fs');
const vm = require('vm');

const elements = {};
const store = {};
let replacedUrl = '';
let deletedCaches = [];
let unregistered = 0;
let confirmTitle = '';
let confirmMessage = '';
let confirmCallback = null;
let toastMessage = '';
const RealDate = Date;
class FixedDate extends RealDate {
  static now() { return 1234567890; }
}

function element(id) {
  if (!elements[id]) {
    elements[id] = {
      id,
      textContent: '',
      innerHTML: '',
      className: '',
      style: {},
      classList: { add() {}, remove() {}, toggle() {} },
    };
  }
  return elements[id];
}

const sandbox = {
  console,
  Date: FixedDate,
  location: { pathname: '/', search: '', replace(url) { replacedUrl = url; } },
  localStorage: {
    getItem(key) { return Object.prototype.hasOwnProperty.call(store, key) ? store[key] : null; },
    setItem(key, value) { store[key] = String(value); },
    removeItem(key) { delete store[key]; },
  },
  caches: {
    async keys() { return ['ot-tracker-v17', 'ot-tracker-v18']; },
    async delete(key) { deletedCaches.push(key); return true; },
  },
  navigator: {
    serviceWorker: {
      async getRegistrations() {
        return [
          { async unregister() { unregistered += 1; return true; } },
          { async unregister() { unregistered += 1; return true; } },
        ];
      },
      register() { return Promise.resolve(); },
    },
  },
  setTimeout(fn) { fn(); },
  document: {
    getElementById: element,
    querySelectorAll() { return []; },
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/auth.js', 'frontend/js/app.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];
store.ot_token = 'token-123';
store.ot_user = JSON.stringify({ username: 'u1', displayName: 'Alice', role: 'user' });

sandbox.OT.showConfirmModal = sandbox.showConfirmModal = function(title, message, onOk) {
  confirmTitle = title;
  confirmMessage = message;
  confirmCallback = onOk;
};
sandbox.OT.showToast = sandbox.showToast = function(message) { toastMessage = message; };

const headerHtml = fs.readFileSync('frontend/index.html', 'utf8').match(/<div class="header-btns">[\s\S]*?<\/div>/)[0];
if (!headerHtml.includes('clearAppCache()')) {
  throw new Error(`Expected header cache refresh button: ${headerHtml}`);
}

sandbox.OT.confirmClearAppCache();
if (confirmTitle !== '刷新应用缓存') throw new Error(`Unexpected confirm title ${confirmTitle}`);
if (!confirmMessage.includes('不会删除服务器记录')) throw new Error(`Confirm message should explain data safety: ${confirmMessage}`);
if (typeof confirmCallback !== 'function') throw new Error('Expected confirm callback');

await confirmCallback();

if (JSON.stringify(deletedCaches) !== JSON.stringify(['ot-tracker-v17', 'ot-tracker-v18'])) {
  throw new Error(`Expected both caches deleted, got ${JSON.stringify(deletedCaches)}`);
}
if (unregistered !== 2) throw new Error(`Expected 2 service workers unregistered, got ${unregistered}`);
if (store.ot_token !== 'token-123') throw new Error('Login token should be preserved');
if (!store.ot_user) throw new Error('Login user should be preserved');
if (!toastMessage.includes('缓存已清理')) throw new Error(`Expected success toast, got ${toastMessage}`);
if (replacedUrl !== '/?refresh=1234567890') throw new Error(`Expected timestamp refresh navigation, got ${replacedUrl}`);
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


def test_frontend_declares_visible_app_version():
    index = (ROOT / "frontend" / "index.html").read_text(encoding="utf-8")
    utils = (ROOT / "frontend" / "js" / "utils.js").read_text(encoding="utf-8")
    css = (ROOT / "frontend" / "css" / "style.css").read_text(encoding="utf-8")

    assert "OT.APP_VERSION = '1.0'" in utils
    assert 'id="app-version"' in index
    assert "v1.0" in index
    assert ".app-version" in css
