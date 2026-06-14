# Bulk Member Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a batch member whitelist import flow so administrators can paste multiple names, skip duplicates, and keep the existing self-registration model.

**Architecture:** The backend gets one explicit admin-only `/api/whitelist/bulk` endpoint that inserts new whitelist rows and reports skipped names. The frontend adds a compact modal around the current member management card and keeps parsing logic in a small pure function for reliable tests. Static cache versioning is bumped with the frontend change.

**Tech Stack:** Python Flask, SQLite, pytest, native HTML/CSS/JavaScript, Node VM tests under pytest, service worker static cache.

---

## File Structure

- Modify `backend/app.py`: add the bulk whitelist route beside the existing single-name whitelist routes.
- Create `tests/test_whitelist_bulk_import.py`: backend API tests with isolated temporary SQLite databases.
- Modify `frontend/index.html`: add the `批量添加` button and bump script query strings from `v=18` to `v=19`.
- Modify `frontend/js/admin.js`: add name parsing, modal rendering, and submit behavior.
- Modify `frontend/js/app.js`: export the new member-import functions to `window`.
- Modify `frontend/sw.js`: bump `CACHE_NAME` and static asset URLs from `v=18` to `v=19`.
- Modify `tests/test_frontend_static_assets.py`: update cache-version expectations and assert the member page exposes the batch button.
- Create `tests/test_frontend_bulk_members.py`: Node VM tests for parsing and submit behavior.
- Modify `管理员使用指南.html`: document single and batch whitelist additions.
- Modify `README.md`: mention batch whitelist additions in the setup/member flow.

---

### Task 1: Backend Bulk Whitelist API

**Files:**
- Create: `tests/test_whitelist_bulk_import.py`
- Modify: `backend/app.py`

- [x] **Step 1: Write the failing backend tests**

Create `tests/test_whitelist_bulk_import.py`:

```python
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


def auth_headers(token):
    return {"Authorization": f"Bearer {token}"}


def login(client, username, password):
    response = client.post("/api/login", json={"username": username, "password": password})
    assert response.status_code == 200
    return response.get_json()["token"]


def whitelist_names(db_path):
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    rows = conn.execute("SELECT name, used FROM whitelist ORDER BY name").fetchall()
    conn.close()
    return [(row["name"], row["used"]) for row in rows]


def add_single_whitelist(client, token, name):
    response = client.post(
        "/api/whitelist",
        json={"name": name},
        headers=auth_headers(token),
    )
    assert response.status_code == 200


def create_member(client, admin_token, display_name):
    add_single_whitelist(client, admin_token, display_name)
    response = client.post(
        "/api/register",
        json={"displayName": display_name, "password": "secret123"},
    )
    assert response.status_code == 200
    return login(client, display_name, "secret123")


def test_bulk_whitelist_adds_unique_names_and_reports_skips(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, db_path):
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        add_single_whitelist(client, admin_token, "王五")

        response = client.post(
            "/api/whitelist/bulk",
            json={"names": [" 张三 ", "李四", "张三", "王五", "   "]},
            headers=auth_headers(admin_token),
        )

        assert response.status_code == 200
        body = response.get_json()
        assert body["added"] == ["张三", "李四"]
        assert body["skippedExisting"] == ["王五"]
        assert body["skippedDuplicate"] == ["张三"]
        assert body["invalid"] == [""]
        assert body["counts"] == {"added": 2, "skipped": 2, "invalid": 1}
        assert whitelist_names(db_path) == [("张三", 0), ("李四", 0), ("王五", 0)]


def test_bulk_whitelist_requires_admin(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _db_path):
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")
        member_token = create_member(client, admin_token, "普通成员")

        unauth = client.post("/api/whitelist/bulk", json={"names": ["张三"]})
        forbidden = client.post(
            "/api/whitelist/bulk",
            json={"names": ["李四"]},
            headers=auth_headers(member_token),
        )

        assert unauth.status_code == 401
        assert forbidden.status_code == 403


def test_bulk_whitelist_rejects_empty_name_list(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _db_path):
        client = app_module.app.test_client()
        admin_token = login(client, "admin", "admin123")

        response = client.post(
            "/api/whitelist/bulk",
            json={"names": []},
            headers=auth_headers(admin_token),
        )

        assert response.status_code == 400
        assert response.get_json()["error"] == "请输入成员姓名"
```

- [x] **Step 2: Run backend tests to verify RED**

Run:

```powershell
python -m pytest tests/test_whitelist_bulk_import.py -q --basetemp .pytest_tmp
```

Expected: FAIL with `404 NOT FOUND` or equivalent because `/api/whitelist/bulk` does not exist yet.

- [x] **Step 3: Implement the backend route**

In `backend/app.py`, add this route after `api_add_whitelist` and before `api_delete_whitelist`:

```python
@app.route('/api/whitelist/bulk', methods=['POST'])
@admin_required
def api_add_whitelist_bulk():
    d = request.json or {}
    raw_names = d.get('names')
    if not isinstance(raw_names, list) or not raw_names:
        return jsonify(error="请输入成员姓名"), 400

    added = []
    skipped_existing = []
    skipped_duplicate = []
    invalid = []
    seen = set()
    conn = get_db()
    try:
        now = int(time.time() * 1000)
        for raw in raw_names:
            name = str(raw or '').strip()
            if not name:
                invalid.append("")
                continue
            if name in seen:
                skipped_duplicate.append(name)
                continue
            seen.add(name)
            if conn.execute("SELECT 1 FROM whitelist WHERE name=?", (name,)).fetchone():
                skipped_existing.append(name)
                continue
            conn.execute(
                "INSERT INTO whitelist (name,used,created_at) VALUES (?,0,?)",
                (name, now),
            )
            added.append(name)
        conn.commit()
        return jsonify(
            ok=True,
            added=added,
            skippedExisting=skipped_existing,
            skippedDuplicate=skipped_duplicate,
            invalid=invalid,
            counts={
                "added": len(added),
                "skipped": len(skipped_existing) + len(skipped_duplicate),
                "invalid": len(invalid),
            },
        )
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()
```

- [x] **Step 4: Run backend tests to verify GREEN**

Run:

```powershell
python -m pytest tests/test_whitelist_bulk_import.py -q --basetemp .pytest_tmp
```

Expected: PASS.

---

### Task 2: Frontend Bulk Modal and Parser

**Files:**
- Create: `tests/test_frontend_bulk_members.py`
- Modify: `frontend/index.html`
- Modify: `frontend/js/admin.js`
- Modify: `frontend/js/app.js`

- [x] **Step 1: Write the failing frontend tests**

Create `tests/test_frontend_bulk_members.py`:

```python
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

const names = sandbox.OT.parseBulkWhitelistNames('张三\\n李四、王五,赵六;钱七；孙八\\n欧阳 娜娜');
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

element('wl-bulk-input').value = '张三\\n李四、张三';

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
```

- [x] **Step 2: Run frontend tests to verify RED**

Run:

```powershell
python -m pytest tests/test_frontend_bulk_members.py -q --basetemp .pytest_tmp
```

Expected: FAIL because `OT.parseBulkWhitelistNames` and `OT.submitBulkWhitelist` are not defined.

- [x] **Step 3: Add frontend implementation**

In `frontend/index.html`, change the member add row to include a second button:

```html
<button class="btn btn-primary btn-sm" onclick="addWhitelist()" style="width:auto;flex-shrink:0">+ 添加</button>
<button class="btn btn-outline btn-sm" onclick="showBulkWhitelistModal()" style="width:auto;flex-shrink:0">批量添加</button>
```

In `frontend/js/admin.js`, add these functions immediately after `OT.addWhitelist`:

```javascript
OT.parseBulkWhitelistNames = function parseBulkWhitelistNames(text){
  return String(text||'').split(/[\r\n、,;；]+/).map(n=>n.trim()).filter(Boolean);
};

OT.showBulkWhitelistModal = function showBulkWhitelistModal(){
  showModal(`<div class="modal-title">批量添加成员</div>
    <div class="export-modal-field"><label class="export-modal-label">成员名单</label><textarea id="wl-bulk-input" class="export-modal-select" rows="8" placeholder="一行一个姓名，也可用顿号、逗号或分号分隔"></textarea></div>
    <div class="note-text">重复姓名会自动跳过；这里只加入白名单，成员仍需自行注册。</div>
    <div class="btn-group"><button class="btn btn-outline" onclick="closeModalDirect()">取消</button><button class="btn btn-primary" id="wl-bulk-submit">确认添加</button></div>`);
  document.getElementById('wl-bulk-submit').onclick=OT.submitBulkWhitelist;
};

OT.submitBulkWhitelist = async function submitBulkWhitelist(){
  const input=document.getElementById('wl-bulk-input');
  const names=OT.parseBulkWhitelistNames(input&&input.value);
  if(!names.length){showToast('请输入成员姓名');return}
  try{
    const d=await api('/whitelist/bulk',{method:'POST',body:JSON.stringify({names})});
    closeModalDirect();
    await OT.loadWhitelist();
    const counts=d.counts||{};
    const added=Number(counts.added)||0;
    const skipped=Number(counts.skipped)||0;
    const invalid=Number(counts.invalid)||0;
    let msg=added?`已添加 ${added} 人`:'没有新增成员';
    if(skipped||invalid)msg+=`，跳过 ${skipped+invalid} 人`;
    showToast(msg);
  }catch(e){showToast('❌ '+e.message)}
};
```

In `frontend/js/app.js`, export these properties in `Object.assign(window, { ... })`:

```javascript
parseBulkWhitelistNames: OT.parseBulkWhitelistNames,
showBulkWhitelistModal: OT.showBulkWhitelistModal,
submitBulkWhitelist: OT.submitBulkWhitelist,
```

- [x] **Step 4: Run frontend tests to verify GREEN**

Run:

```powershell
python -m pytest tests/test_frontend_bulk_members.py -q --basetemp .pytest_tmp
```

Expected: PASS.

---

### Task 3: Static Cache Version and Documentation

**Files:**
- Modify: `frontend/index.html`
- Modify: `frontend/sw.js`
- Modify: `tests/test_frontend_static_assets.py`
- Modify: `管理员使用指南.html`
- Modify: `README.md`

- [x] **Step 1: Write failing static/docs assertions**

In `tests/test_frontend_static_assets.py`, update `test_static_asset_cache_version_is_current_and_consistent` to reject `v=18`, require `ot-tracker-v19`, and require `?v=19` for each static asset.

Add this test near the other static member/admin assertions:

```python
def test_members_page_exposes_bulk_whitelist_addition():
    index = (ROOT / "frontend" / "index.html").read_text(encoding="utf-8")
    guide = (ROOT / "管理员使用指南.html").read_text(encoding="utf-8")
    readme = (ROOT / "README.md").read_text(encoding="utf-8")

    assert "批量添加" in index
    assert "一行一个姓名" in guide
    assert "重复姓名会自动跳过" in guide
    assert "批量添加成员白名单" in readme
```

- [x] **Step 2: Run static/docs tests to verify RED**

Run:

```powershell
python -m pytest tests/test_frontend_static_assets.py::test_static_asset_cache_version_is_current_and_consistent tests/test_frontend_static_assets.py::test_members_page_exposes_bulk_whitelist_addition -q --basetemp .pytest_tmp
```

Expected: FAIL because the cache is still `v=18` and docs do not mention batch additions yet.

- [x] **Step 3: Update cache version and docs**

In `frontend/index.html`, change every `?v=18` script URL to `?v=19`.

In `frontend/sw.js`, change:

```javascript
const CACHE_NAME = 'ot-tracker-v19';
```

and change every static asset query string from `?v=18` to `?v=19`.

In `管理员使用指南.html`, under 「添加白名单」, replace the single-add-only wording with:

```html
<div class="step">
  <div class="step-num">2</div>
  <div class="step-text">少量人员可在输入框输入<strong>成员姓名</strong>，点击「+ 添加」；多人名单可点击「批量添加」，按一行一个姓名粘贴，也可用顿号、逗号或分号分隔</div>
</div>
<div class="step">
  <div class="step-num">3</div>
  <div class="step-text">系统会自动跳过重复姓名和已存在姓名，只新增未在白名单中的成员</div>
</div>
<div class="step">
  <div class="step-num">4</div>
  <div class="step-text">通知成员使用自己的姓名注册；白名单只控制注册资格，不直接生成账号或密码</div>
</div>
```

In `README.md`, change setup step 5 to:

```markdown
5. 添加或批量添加成员白名单
```

and change the member registration step to:

```markdown
1. 管理员在白名单中添加成员姓名；多人名单可批量添加成员白名单，推荐一行一个姓名，重复姓名会自动跳过
```

- [x] **Step 4: Run static/docs tests to verify GREEN**

Run:

```powershell
python -m pytest tests/test_frontend_static_assets.py -q --basetemp .pytest_tmp
```

Expected: PASS.

---

### Task 4: Full Verification and Commit

**Files:**
- All changed files from Tasks 1-3.

- [x] **Step 1: Run focused regression tests**

Run:

```powershell
python -m pytest tests/test_whitelist_bulk_import.py tests/test_frontend_bulk_members.py tests/test_frontend_static_assets.py -q --basetemp .pytest_tmp
```

Expected: PASS.

- [x] **Step 2: Run full test suite**

Run:

```powershell
python -m pytest tests -q --basetemp .pytest_tmp
```

Expected: PASS, matching the clean baseline with the new tests included.

- [x] **Step 3: Inspect git diff**

Run:

```powershell
git status --short
git diff --stat
git diff --check
```

Expected: only the planned overtime-tracker files changed, no whitespace errors.

- [x] **Step 4: Commit**

Run:

```powershell
git add overtime-tracker/backend/app.py overtime-tracker/frontend/index.html overtime-tracker/frontend/js/admin.js overtime-tracker/frontend/js/app.js overtime-tracker/frontend/sw.js overtime-tracker/tests/test_whitelist_bulk_import.py overtime-tracker/tests/test_frontend_bulk_members.py overtime-tracker/tests/test_frontend_static_assets.py overtime-tracker/管理员使用指南.html overtime-tracker/README.md overtime-tracker/docs/superpowers/plans/2026-06-14-bulk-member-import.md
git commit -m "feat: add bulk member import"
```

Expected: commit succeeds on branch `codex/overtime-bulk-members`.
