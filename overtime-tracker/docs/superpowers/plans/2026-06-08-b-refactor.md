# 考勤助手 B 方案 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved B方案: security hardening, clearer clock feedback, remote marker export, dashboard period linkage, and a maintainable native-JS frontend split.

**Architecture:** Keep the Flask + SQLite backend API stable, adding password/JWT helpers and small validation changes in `backend/app.py`. Split the current single-file frontend into static CSS and ordered global scripts under `frontend/css` and `frontend/js`, using `window.OT` as the shared namespace without adding a build step.

**Tech Stack:** Python Flask, SQLite, PyJWT, bcrypt, pytest, native HTML/CSS/JavaScript, Docker/Nginx static serving.

---

## Scope Check

This plan is one coherent sub-project. Each task is independently testable and keeps the existing deployment shape unchanged.

The repository root `D:\Documents\GitHub\minis` may contain unrelated dirty changes in sibling projects. Do not stage or modify those files. Work only inside `D:\Documents\GitHub\minis\overtime-tracker`.

## File Structure

- Modify: `D:\Documents\GitHub\minis\overtime-tracker\backend\app.py`
  Backend security helpers, auth endpoints, note validation, and export payload metadata.
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\backend\requirements.txt`
  Add `bcrypt` and test dependencies if not already available locally.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_auth_security.py`
  JWT secret persistence, bcrypt creation, and legacy SHA256 migration tests.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_clock_rules.py`
  Required note validation for both clock-in and clock-out.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_export_payload.py`
  Verify export payload keeps `out_of_range` markers for frontend CSV generation.
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\index.html`
  Keep DOM structure, remove inline CSS/JS, load external assets in the approved order, add small UI placeholders for feedback and period linkage.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\css\style.css`
  Existing stylesheet extracted from `index.html`, plus small additions for the last-record and in-progress status UI.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\utils.js`
  `window.OT`, API wrapper, escaping, time helpers, modal/toast/download helpers.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\auth.js`
  Login, register, logout, role navigation setup.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\admin.js`
  Dashboard, unified export, members, settings, email, location, and holiday admin code.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\clock.js`
  Geolocation, clocking, required reason prompt, last-record feedback, and in-progress status.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\records.js`
  Calendar, records list, day detail, and user export.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\stats.js`
  User summary stats, monthly chart, and shared duration calculations.
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\app.js`
  App bootstrap, path detection, page routing, service worker registration.

## Task 1: Backend Security Tests

**Files:**
- Create: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_auth_security.py`
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\backend\requirements.txt`

- [ ] **Step 1: Add test dependencies**

Add these lines to `backend\requirements.txt` if absent:

```txt
bcrypt==4.1.3
pytest==8.2.2
```

- [ ] **Step 2: Write failing auth security tests**

Create `tests\test_auth_security.py`:

```python
import hashlib
import importlib
import os
import sqlite3
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BACKEND = ROOT / "backend"


def load_app(tmp_path, monkeypatch):
    data_dir = tmp_path / "data"
    data_dir.mkdir()
    monkeypatch.setenv("DB_PATH", str(data_dir / "overtime.db"))
    monkeypatch.delenv("JWT_SECRET", raising=False)
    if str(BACKEND) not in sys.path:
        sys.path.insert(0, str(BACKEND))
    sys.modules.pop("app", None)
    module = importlib.import_module("app")
    module.app.config.update(TESTING=True)
    return module


def user_row(module, username):
    conn = module.get_db()
    row = conn.execute("SELECT * FROM users WHERE username=?", (username,)).fetchone()
    conn.close()
    return row


def test_default_admin_uses_bcrypt(tmp_path, monkeypatch):
    module = load_app(tmp_path, monkeypatch)
    row = user_row(module, "admin")

    assert row["password_hash"].startswith("$2b$")
    assert module.verify_pw("admin123", row)


def test_legacy_sha256_password_migrates_after_login(tmp_path, monkeypatch):
    module = load_app(tmp_path, monkeypatch)
    salt = "abc123"
    legacy_hash = hashlib.sha256(("secret" + salt).encode()).hexdigest()
    conn = module.get_db()
    conn.execute(
        "INSERT INTO users VALUES (?,?,?,?,?,?)",
        ("legacy_user", "Legacy User", legacy_hash, salt, "user", 1),
    )
    conn.commit()
    conn.close()

    client = module.app.test_client()
    response = client.post("/api/login", json={"username": "legacy_user", "password": "secret"})

    assert response.status_code == 200
    migrated = user_row(module, "legacy_user")
    assert migrated["password_hash"].startswith("$2b$")
    assert migrated["salt"] == ""
    assert module.verify_pw("secret", migrated)


def test_jwt_secret_persists_in_data_dir(tmp_path, monkeypatch):
    first = load_app(tmp_path, monkeypatch)
    first_secret = first.SECRET
    secret_file = Path(os.environ["DB_PATH"]).parent / "jwt_secret"

    assert secret_file.exists()
    assert secret_file.read_text(encoding="utf-8") == first_secret

    second = load_app(tmp_path, monkeypatch)

    assert second.SECRET == first_secret
```

- [ ] **Step 3: Run tests and verify they fail for missing bcrypt helpers**

Run:

```powershell
cd "D:\Documents\GitHub\minis\overtime-tracker"
python -m pytest tests\test_auth_security.py -q
```

Expected: FAIL because `verify_pw` is not defined and default admin still uses SHA256.

## Task 2: Backend Security Implementation

**Files:**
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\backend\app.py`
- Test: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_auth_security.py`

- [ ] **Step 1: Add bcrypt import and persistent secret loader**

In `backend\app.py`, change imports and replace the current `SECRET` line:

```python
import os, json, hashlib, time, sqlite3, smtplib, secrets, math
import bcrypt
```

Use this helper before `SECRET` is assigned:

```python
def load_jwt_secret():
    env_secret = os.environ.get("JWT_SECRET")
    if env_secret:
        return env_secret
    data_dir = os.path.dirname(DB_PATH)
    os.makedirs(data_dir, exist_ok=True)
    secret_path = os.path.join(data_dir, "jwt_secret")
    if os.path.exists(secret_path):
        with open(secret_path, "r", encoding="utf-8") as f:
            return f.read().strip()
    secret = secrets.token_hex(32)
    with open(secret_path, "w", encoding="utf-8") as f:
        f.write(secret)
    return secret
```

Then assign:

```python
DB_PATH = os.environ.get('DB_PATH', '/data/overtime.db')
SECRET = load_jwt_secret()
```

- [ ] **Step 2: Replace password helpers**

Replace `hash_pw` with:

```python
def hash_pw(pw, salt=None):
    return bcrypt.hashpw(pw.encode("utf-8"), bcrypt.gensalt(rounds=12)).decode("utf-8")


def is_bcrypt_hash(value):
    return isinstance(value, str) and value.startswith(("$2a$", "$2b$", "$2y$"))


def verify_pw(pw, user_row):
    stored = user_row["password_hash"]
    if is_bcrypt_hash(stored):
        return bcrypt.checkpw(pw.encode("utf-8"), stored.encode("utf-8"))
    legacy = hashlib.sha256((pw + (user_row["salt"] or "")).encode()).hexdigest()
    return legacy == stored


def migrate_password_if_needed(conn, user_row, pw):
    if is_bcrypt_hash(user_row["password_hash"]):
        return
    conn.execute(
        "UPDATE users SET password_hash=?, salt='' WHERE username=?",
        (hash_pw(pw), user_row["username"]),
    )
    conn.commit()
```

- [ ] **Step 3: Update default admin creation**

Change the default admin block:

```python
if not conn.execute("SELECT 1 FROM users WHERE username='admin'").fetchone():
    conn.execute(
        "INSERT INTO users VALUES (?,?,?,?,?,?)",
        ("admin", "管理员", hash_pw("admin123"), "", "admin", int(time.time() * 1000)),
    )
    conn.commit()
```

- [ ] **Step 4: Update login migration**

In `api_login`, keep the connection open until verification and migration complete:

```python
u = conn.execute("SELECT * FROM users WHERE username=? OR display_name=?", (login_id, login_id)).fetchone()
if not u or not verify_pw(d.get("password", ""), u):
    conn.close()
    return jsonify(error="用户名或密码错误"), 401
migrate_password_if_needed(conn, u, d.get("password", ""))
conn.close()
return jsonify(token=make_token(u), user={"username": u["username"], "displayName": u["display_name"], "role": u["role"]})
```

- [ ] **Step 5: Update register/change/reset password writes**

For register:

```python
conn.execute(
    "INSERT INTO users VALUES (?,?,?,?,?,?)",
    (username, display, hash_pw(password), "", "user", int(time.time() * 1000)),
)
```

For `api_change_password`, replace the old password check and update:

```python
if not verify_pw(old_pw, u):
    conn.close()
    return jsonify(error="当前密码错误"), 400
h = hash_pw(new_pw)
conn.execute("UPDATE users SET password_hash=?, salt='' WHERE username=?", (h, uid))
```

For `api_reset_password`:

```python
h = hash_pw(new_pw)
conn.execute("UPDATE users SET password_hash=?, salt='' WHERE username=?", (h, username))
```

- [ ] **Step 6: Run auth tests**

Run:

```powershell
cd "D:\Documents\GitHub\minis\overtime-tracker"
python -m pytest tests\test_auth_security.py -q
```

Expected: PASS.

- [ ] **Step 7: Commit backend security**

Run:

```powershell
git add backend\app.py backend\requirements.txt tests\test_auth_security.py
git commit -m "feat: harden authentication storage"
```

## Task 3: Clock Rule Tests and Implementation

**Files:**
- Create: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_clock_rules.py`
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\backend\app.py`

- [ ] **Step 1: Write failing clock note tests**

Create `tests\test_clock_rules.py`:

```python
import importlib
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BACKEND = ROOT / "backend"


def load_app(tmp_path, monkeypatch):
    data_dir = tmp_path / "data"
    data_dir.mkdir()
    monkeypatch.setenv("DB_PATH", str(data_dir / "overtime.db"))
    if str(BACKEND) not in sys.path:
        sys.path.insert(0, str(BACKEND))
    sys.modules.pop("app", None)
    module = importlib.import_module("app")
    module.app.config.update(TESTING=True)
    return module


def token_for(client, username, password):
    resp = client.post("/api/login", json={"username": username, "password": password})
    assert resp.status_code == 200
    return resp.get_json()["token"]


def create_member(module, display_name="张三", password="pass123"):
    conn = module.get_db()
    conn.execute("INSERT INTO whitelist (name,used,created_at) VALUES (?,0,?)", (display_name, int(time.time() * 1000)))
    conn.commit()
    conn.close()
    client = module.app.test_client()
    resp = client.post("/api/register", json={"displayName": display_name, "password": password})
    assert resp.status_code == 200
    return token_for(client, display_name, password)


def configure_location(module):
    conn = module.get_db()
    settings = module.load_settings_from_conn(conn)
    settings.update({"lat": 31.2304, "lng": 121.4737, "radius": 500, "gpsAccuracy": 100})
    conn.execute("UPDATE settings SET data=? WHERE id=1", (module.json.dumps(settings),))
    conn.commit()
    conn.close()


def test_clock_in_requires_note(tmp_path, monkeypatch):
    module = load_app(tmp_path, monkeypatch)
    configure_location(module)
    client = module.app.test_client()
    token = create_member(module)

    resp = client.post(
        "/api/clock",
        headers={"Authorization": "Bearer " + token},
        json={"type": "in", "lat": 31.2304, "lng": 121.4737, "accuracy": 20, "note": "   "},
    )

    assert resp.status_code == 400
    assert "事由" in resp.get_json()["error"]


def test_clock_out_requires_note(tmp_path, monkeypatch):
    module = load_app(tmp_path, monkeypatch)
    configure_location(module)
    client = module.app.test_client()
    token = create_member(module)
    headers = {"Authorization": "Bearer " + token}
    ok = client.post(
        "/api/clock",
        headers=headers,
        json={"type": "in", "lat": 31.2304, "lng": 121.4737, "accuracy": 20, "note": "项目记录"},
    )
    assert ok.status_code == 200

    resp = client.post(
        "/api/clock",
        headers=headers,
        json={"type": "out", "lat": 31.2304, "lng": 121.4737, "accuracy": 20, "note": ""},
    )

    assert resp.status_code == 400
    assert "事由" in resp.get_json()["error"]
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
cd "D:\Documents\GitHub\minis\overtime-tracker"
python -m pytest tests\test_clock_rules.py -q
```

Expected: FAIL because `/api/clock` accepts empty notes.

- [ ] **Step 3: Enforce note validation in backend**

In `api_clock`, normalize note before inserting:

```python
note = (d.get("note") or "").strip()
if not note:
    conn.close()
    return jsonify(error="请填写事由"), 400
note = note[:500]
```

Remove the existing line:

```python
note = (d.get('note') or '')[:500]
```

- [ ] **Step 4: Run clock tests**

Run:

```powershell
python -m pytest tests\test_clock_rules.py -q
```

Expected: PASS.

- [ ] **Step 5: Commit clock rule**

Run:

```powershell
git add backend\app.py tests\test_clock_rules.py
git commit -m "feat: require clock note"
```

## Task 4: Export Payload Test

**Files:**
- Create: `D:\Documents\GitHub\minis\overtime-tracker\tests\test_export_payload.py`
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\backend\app.py`

- [ ] **Step 1: Write export marker test**

Create `tests\test_export_payload.py`:

```python
import importlib
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BACKEND = ROOT / "backend"


def load_app(tmp_path, monkeypatch):
    data_dir = tmp_path / "data"
    data_dir.mkdir()
    monkeypatch.setenv("DB_PATH", str(data_dir / "overtime.db"))
    if str(BACKEND) not in sys.path:
        sys.path.insert(0, str(BACKEND))
    sys.modules.pop("app", None)
    module = importlib.import_module("app")
    module.app.config.update(TESTING=True)
    return module


def login(client, username, password):
    resp = client.post("/api/login", json={"username": username, "password": password})
    assert resp.status_code == 200
    return resp.get_json()["token"]


def test_export_records_include_remote_marker_source(tmp_path, monkeypatch):
    module = load_app(tmp_path, monkeypatch)
    client = module.app.test_client()
    conn = module.get_db()
    conn.execute(
        "INSERT INTO users VALUES (?,?,?,?,?,?)",
        ("u1", "张三", module.hash_pw("pass123"), "", "user", int(time.time() * 1000)),
    )
    conn.execute(
        "INSERT INTO records (user_id,date,time_str,ts,type,lat,lng,accuracy,out_of_range,note) VALUES (?,?,?,?,?,?,?,?,?,?)",
        ("u1", "2026-06-08", "19:00:00", 1780935600000, "in", 31.0, 121.0, 20, 1, "远程说明"),
    )
    conn.commit()
    conn.close()

    token = login(client, "张三", "pass123")
    resp = client.get("/api/export?period=all", headers={"Authorization": "Bearer " + token})

    assert resp.status_code == 200
    records = resp.get_json()["records"]
    assert records[0]["out_of_range"] == 1
    assert records[0]["note"] == "远程说明"
```

- [ ] **Step 2: Run export payload test**

Run:

```powershell
python -m pytest tests\test_export_payload.py -q
```

Expected: PASS if the current export endpoint already includes `out_of_range` and `note`. If it fails because SQL omits columns, update the export SELECT queries to keep `r.*`.

- [ ] **Step 3: Commit test**

Run:

```powershell
git add tests\test_export_payload.py
git commit -m "test: cover export remote marker payload"
```

## Task 5: Extract Frontend Assets

**Files:**
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\index.html`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\css\style.css`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\utils.js`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\auth.js`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\admin.js`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\clock.js`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\records.js`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\stats.js`
- Create: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\app.js`

- [ ] **Step 1: Extract CSS**

Move the contents currently inside `<style>...</style>` into `frontend\css\style.css`. In `frontend\index.html`, replace the removed style block with:

```html
<link rel="stylesheet" href="/css/style.css">
```

- [ ] **Step 2: Create `utils.js` with shared state and helpers**

Create `frontend\js\utils.js` with the moved global variables and helpers:

```javascript
window.OT = window.OT || {};

OT.API_BASE = location.origin;
OT.token = localStorage.getItem('ot_token') || null;
OT.currentUser = JSON.parse(localStorage.getItem('ot_user') || 'null');
OT.settings = {};
OT.allRecords = [];
OT.currentPos = null;
OT.calMonth = new Date().getMonth();
OT.calYear = new Date().getFullYear();
OT.clockIntervalId = null;

OT.escapeHtml = function(v){return String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))};
OT.safeToken = function(v){return encodeURIComponent(String(v??'')).replace(/'/g,'%27')};
OT.csvCell = function(v){return `"${String(v??'').replace(/"/g,'""')}"`};
OT.api = async function(path, opts={}) {
  const headers = {'Content-Type':'application/json', ...(opts.headers||{})};
  if (OT.token) headers['Authorization'] = 'Bearer ' + OT.token;
  const resp = await fetch(OT.API_BASE+'/api'+path, {...opts, headers});
  const data = await resp.json();
  if (!resp.ok) throw new Error(data.error||'请求失败');
  return data;
};
OT.bjNow = function(){return new Date(Date.now()+8*3600000)};
OT.bjTimeStr = function(d){return d.toISOString().slice(11,19)};
OT.bjDateStr = function(d){return d.toISOString().slice(0,10)};
OT.bjWeekday = function(d){return['周日','周一','周二','周三','周四','周五','周六'][d.getUTCDay()]};
OT.dateKey = function(d){const x=d?new Date(d.getTime()+8*3600000):OT.bjNow();return x.getUTCFullYear()+'-'+String(x.getUTCMonth()+1).padStart(2,'0')+'-'+String(x.getUTCDate()).padStart(2,'0')};
OT.calendarKey = function(d){return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')};
OT.timeToMin = function(t){if(!t)return 540;const[h,m]=t.split(':');return +h*60+(+m)};
OT.msToMin = function(ts,timeStr){if(timeStr){const p=timeStr.split(':');return(+p[0])*60+(+p[1])}const d=new Date(ts+8*3600000);return d.getUTCHours()*60+d.getUTCMinutes()};
OT.formatMinutes = function(m){if(m<=0)return'0 分钟';const h=Math.floor(m/60),mm=m%60;return h===0?`${mm} 分钟`:mm===0?`${h} 小时`:`${h} 小时 ${mm} 分钟`};
OT.fmtMin = function(m){if(m<=0)return'0h';const h=Math.floor(m/60),mm=m%60;return mm===0?`${h}h`:`${h}h${mm}m`};
OT.downloadBlob = function(b,n){const u=URL.createObjectURL(b);const a=document.createElement('a');a.href=u;a.download=n;a.click();URL.revokeObjectURL(u)};
OT.showToast = function(msg){const t=document.getElementById('toast');t.textContent=msg;t.classList.add('show');setTimeout(()=>t.classList.remove('show'),2000)};
OT.showModal = function(html){document.getElementById('modal-body').innerHTML=html;document.getElementById('modal').classList.add('show')};
OT.closeModal = function(e){if(e.target===document.getElementById('modal'))document.getElementById('modal').classList.remove('show')};
OT.closeModalDirect = function(){document.getElementById('modal').classList.remove('show')};
OT.showConfirmModal = function(title,msg,onOk){OT.showModal(`<div class="modal-title">${OT.escapeHtml(title)}</div><p style="text-align:center;color:var(--text-sec);margin-bottom:16px;white-space:pre-line">${OT.escapeHtml(msg)}</p><div class="btn-group"><button class="btn btn-outline" onclick="OT.closeModalDirect()">取消</button><button class="btn btn-primary" id="modal-ok-btn">确认</button></div>`);document.getElementById('modal-ok-btn').onclick=()=>{OT.closeModalDirect();onOk()}};

window.closeModal = OT.closeModal;
window.closeModalDirect = OT.closeModalDirect;
```

- [ ] **Step 3: Move functions into domain files**

Move existing inline functions without changing behavior, except replacing globals with `OT.`:

```txt
auth.js:
switchAuthTab, doLogin, doRegister, doLogout, enterApp, buildTabNav, applyRoleAccess

admin.js:
loadDashboard, showExportDialog, doUnifiedExport, doExport, doOldExport, renderSettings,
toggleWeekday, saveSettings, changeMyPassword, toggleSection, loadWhitelist,
addWhitelist, deleteWL, loadUserList, toggleRole, resetPwd, delUser, loadEmailConfig,
toggleEmailEnabled, saveEmailConfig, testEmail, sendReportNow, setCurrentLocation,
confirmLoc, syncHolidays, renderHolidayTags, rmHoliday, addHolidayManual

clock.js:
updateClock, isWithinRange, haversineDistance, startGeoWatch, updateGeoStatus,
updateClockButton, getLastTodayRecord, handleClock, doClock, updateTodayTimeline

records.js:
loadAllRecords, renderCalendar, changeMonth, renderRecordsList, exportMyRecords, showDayDetail

stats.js:
recordPairs, calcTodayOT, isWorkingDay, getWorkMinutes, renderStats, updateHeaderStats

app.js:
showPage, path detection IIFE, token auto-enter, service worker registration
```

At the end of each file, expose functions still used by inline `onclick` handlers:

```javascript
Object.assign(window, {
  switchAuthTab: OT.switchAuthTab,
  doLogin: OT.doLogin,
  doRegister: OT.doRegister
});
```

Use the same pattern in each file for its handlers.

- [ ] **Step 4: Replace inline script with ordered script tags**

At the bottom of `frontend\index.html`, replace the large inline `<script>...</script>` block with:

```html
<script src="/js/utils.js"></script>
<script src="/js/auth.js"></script>
<script src="/js/stats.js"></script>
<script src="/js/records.js"></script>
<script src="/js/clock.js"></script>
<script src="/js/admin.js"></script>
<script src="/js/app.js"></script>
```

The order places `stats.js` before modules that call `calcTodayOT`.

- [ ] **Step 5: Run static smoke checks**

Run:

```powershell
cd "D:\Documents\GitHub\minis\overtime-tracker"
rg -n "<style>|// ==================== API|let token|function api" frontend\index.html
rg -n "window\.OT|OT\.api|Object\.assign\(window" frontend\js
```

Expected:
- First command finds no inline style or old inline API block in `index.html`.
- Second command finds `window.OT`, `OT.api`, and exported handlers in JS files.

- [ ] **Step 6: Commit frontend split**

Run:

```powershell
git add frontend\index.html frontend\css\style.css frontend\js
git commit -m "refactor: split frontend assets"
```

## Task 6: Frontend Clock Feedback

**Files:**
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\index.html`
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\css\style.css`
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\clock.js`

- [ ] **Step 1: Add last-record placeholder**

Inside `#page-clock`, immediately after the clock status div, add:

```html
<div class="last-clock-result" id="last-clock-result" style="display:none"></div>
```

- [ ] **Step 2: Add feedback styles**

Append to `frontend\css\style.css`:

```css
.last-clock-result{margin:14px auto 0;max-width:260px;background:#F8FAFC;border:1px solid var(--border);border-radius:8px;padding:10px;text-align:left;font-size:13px}
.last-clock-result .result-main{font-weight:700;color:var(--primary);margin-bottom:4px}
.last-clock-result .result-sub{color:var(--text-sec);line-height:1.4}
.timeline-status{margin:0 0 12px 16px;background:#EEF2FF;color:var(--primary);border-radius:8px;padding:10px 12px;font-size:13px;font-weight:600}
```

- [ ] **Step 3: Require reason on every clock action**

In `OT.handleClock`, replace the current type-specific reason logic with:

```javascript
const reason=prompt('请输入事由：');
if(reason===null)return;
if(!reason.trim()){OT.showToast('请填写事由');return}
```

Pass `reason.trim()` to `OT.doClock`.

- [ ] **Step 4: Show last clock result**

Add this helper to `clock.js`:

```javascript
OT.showLastClockResult = function(record, durationText){
  const el=document.getElementById('last-clock-result');
  if(!el)return;
  const label=record.type==='in'?'上班记录':'下班记录';
  el.style.display='block';
  el.innerHTML=`<div class="result-main">${label} ${OT.escapeHtml(record.time_str||'')}</div><div class="result-sub">${durationText?`本次工时 ${OT.escapeHtml(durationText)}<br>`:''}${record.out_of_range?'范围外已标记<br>':''}${OT.escapeHtml(record.note||'')}</div>`;
};
```

In `OT.doClock`, after pushing the new record into `OT.allRecords`, compute and display:

```javascript
const record={user_id:OT.currentUser.username,date:d.date,time_str:d.time,ts:Date.now(),type,out_of_range:d.outOfRange?1:0,note};
OT.allRecords.push(record);
let durationText='';
if(type==='out'){
  const todayRecords=OT.allRecords.filter(r=>r.date===d.date);
  durationText=OT.formatMinutes(OT.calcTodayOT(todayRecords,new Date(d.date+'T12:00:00')));
}
OT.showLastClockResult(record,durationText);
```

- [ ] **Step 5: Show in-progress status**

At the top of `OT.updateTodayTimeline`, after sorting `recs`, add:

```javascript
const last=recs[recs.length-1];
let status='';
if(last&&last.type==='in'){
  const elapsed=Math.max(0,Math.round((Date.now()-last.ts)/60000));
  status=`<div class="timeline-status">进行中：${OT.formatMinutes(elapsed)}</div>`;
}
tl.innerHTML=status+recs.map(/* existing mapping */).join('');
```

Keep the existing timeline mapping, using `OT.escapeHtml`.

- [ ] **Step 6: Manual browser check**

Run the app locally:

```powershell
cd "D:\Documents\GitHub\minis\overtime-tracker"
docker compose up -d --build
```

Open `http://localhost:3090`, login, perform one range-valid clock-in with a reason, and verify:
- empty reason is rejected before API call
- last-record box appears after success
- today card shows "进行中"

- [ ] **Step 7: Commit clock feedback**

Run:

```powershell
git add frontend\index.html frontend\css\style.css frontend\js\clock.js
git commit -m "feat: improve clock feedback"
```

## Task 7: Dashboard Linkage and CSV Remote Column

**Files:**
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\index.html`
- Modify: `D:\Documents\GitHub\minis\overtime-tracker\frontend\js\admin.js`

- [ ] **Step 1: Add week option and shared period handler**

In the dashboard export bar, change `export-period` to:

```html
<select id="export-period" onchange="OT.loadDashboard()">
  <option value="month">本月</option>
  <option value="week">本周</option>
  <option value="today">今日</option>
  <option value="all">全部</option>
</select>
```

- [ ] **Step 2: Add period helper to admin.js**

Add:

```javascript
OT.getAdminPeriod = function(){
  const period=document.getElementById('export-period')?.value||'month';
  const today=OT.dateKey(new Date());
  if(period==='today')return {period,label:'今日',from:today,to:today};
  if(period==='week'){
    const start=new Date(today+'T12:00:00');
    start.setDate(start.getDate()-start.getDay()+1);
    return {period,label:'本周',from:OT.calendarKey(start),to:today};
  }
  if(period==='month')return {period,label:'本月',from:today.slice(0,7)+'-01',to:today.slice(0,7)+'-31'};
  return {period,label:'全部',from:'',to:''};
};

OT.inAdminPeriod = function(record, range){
  if(!range.from||!range.to)return true;
  return record.date>=range.from&&record.date<=range.to;
};
```

- [ ] **Step 3: Apply period to dashboard data**

At the start of `OT.loadDashboard`, add:

```javascript
const range=OT.getAdminPeriod();
```

Filter records for table, summary, and ranking with:

```javascript
const scopedRecords=records.filter(r=>OT.inAdminPeriod(r,range));
```

Use `scopedRecords` for the table and ranking. Keep the 30-day trend on all records, since it is explicitly a trend module.

Update labels:

```javascript
<div class="card-title">团队工时概览（${range.label}）</div>
```

and header stats:

```javascript
<div class="stat-item"><div class="stat-value">${OT.fmtMin(totalScopedOT)}</div><div class="stat-label">${range.label}团队工时</div></div>
```

- [ ] **Step 4: Export from the shared dashboard controls**

Change `OT.doExport` for admin users:

```javascript
OT.doExport = async function(){
  if(!(OT.currentUser&&OT.currentUser.role==='admin')){
    OT.showExportDialog();
    return;
  }
  const scope=document.getElementById('export-scope').value;
  const period=document.getElementById('export-period').value;
  await OT.exportCsv(scope, period);
};
```

Extract the common CSV body from `doUnifiedExport` into:

```javascript
OT.exportCsv = async function(uid, period, from='', to=''){
  let url='/export?';
  if(uid&&uid!=='all')url+=`uid=${encodeURIComponent(uid)}&`;
  if(from&&to)url+=`from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
  else if(period&&period!=='all')url+=`period=${encodeURIComponent(period)}`;
  const data=await OT.api(url);
  const recs=data.records||[];
  if(!recs.length){OT.showToast('暂无数据');return}
  const groups={};
  recs.forEach(r=>{
    const key=(r.display_name||r.user_id)+'|'+r.date;
    if(!groups[key])groups[key]={name:r.display_name||r.user_id,date:r.date,records:[]};
    groups[key].records.push(r);
  });
  const header='姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)\n';
  const rows=Object.values(groups).map(g=>{
    const d=new Date(g.date+'T12:00:00');
    const wd=['周日','周一','周二','周三','周四','周五','周六'][d.getDay()];
    const sorted=g.records.sort((a,b)=>a.ts-b.ts);
    const fi=sorted.find(r=>r.type==='in');
    const lo=[...sorted].reverse().find(r=>r.type==='out');
    const reasons=sorted.filter(r=>r.note).map(r=>r.note).filter(Boolean).join('; ');
    const remote=sorted.some(r=>Number(r.out_of_range)===1)?'是':'';
    const isWk=OT.isWorkingDay(d);
    const ot=OT.calcTodayOT(sorted,d);
    const hrs=ot>=60?Math.floor(ot/60)+'h'+(ot%60?ot%60+'m':''):ot+'m';
    return[OT.csvCell(g.name),OT.csvCell(g.date),OT.csvCell(wd),OT.csvCell(fi?(fi.time_str||'').slice(0,5):''),OT.csvCell(lo?(lo.time_str||'').slice(0,5):''),OT.csvCell(isWk?'工作日':'休息日'),OT.csvCell(reasons),OT.csvCell(remote),ot,OT.csvCell(hrs)].join(',');
  }).join('\n');
  OT.downloadBlob(new Blob(['\uFEFF'+header+rows],{type:'text/csv;charset=utf-8'}),`工时报表_${OT.dateKey(OT.bjNow())}.csv`);
  OT.closeModalDirect();
  OT.showToast('已导出');
};
```

Update `doUnifiedExport` to call `OT.exportCsv(uid, period, from, to)`.

- [ ] **Step 5: Verify remote CSV marker manually**

Use a test record with `out_of_range=1`, export CSV, and verify the `远程` column contains `是` for that grouped day.

- [ ] **Step 6: Commit dashboard/export**

Run:

```powershell
git add frontend\index.html frontend\js\admin.js
git commit -m "feat: link dashboard period and export markers"
```

## Task 8: Frontend Smoke Verification

**Files:**
- Modify only if verification finds a regression in files changed above.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
cd "D:\Documents\GitHub\minis\overtime-tracker"
python -m pytest tests -q
```

Expected: all tests PASS.

- [ ] **Step 2: Build and start Docker app**

Run:

```powershell
docker compose up -d --build
```

Expected: container starts and port `127.0.0.1:3090` serves the app.

- [ ] **Step 3: Browser smoke test**

Open `http://localhost:3090` and verify:
- login page loads with external CSS
- admin login works with `admin` / `admin123`
- dashboard loads without console errors
- period selector updates dashboard values
- CSV export downloads with `远程` column
- member page loads
- settings page loads
- user login flow still shows clock/records/stats tabs

- [ ] **Step 4: Sensitive wording scan**

Run:

```powershell
rg -n "加班" frontend backend README.md 使用指南.html 管理员使用指南.html docs
```

Expected: the only allowed matches are historical brainstorming/spec/plan docs if they quote the design discussion. No matches should appear in runtime files: `frontend`, `backend`, `README.md`, `使用指南.html`, `管理员使用指南.html`.

- [ ] **Step 5: Final commit if verification fixes were needed**

If Step 1-4 required fixes, commit them:

```powershell
git add backend frontend tests
git commit -m "fix: resolve verification regressions"
```

## Self-Review

- Spec coverage:
  - Security hardening: Tasks 1-2.
  - Required reason and clock feedback: Tasks 3 and 6.
  - Remote marker in CSV: Tasks 4 and 7.
  - Dashboard period linkage: Task 7.
  - Frontend split: Task 5.
  - Runtime wording scan: Task 8.
- Placeholder scan: no placeholder tokens or deferred requirements are present.
- Type consistency: shared frontend state consistently uses `OT.currentUser`, `OT.settings`, `OT.allRecords`, and helper functions on `OT`.
