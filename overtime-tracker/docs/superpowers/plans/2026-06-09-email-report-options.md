# Email Report Options Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为邮件报表增加发送频率、报表范围、内容形式与范围外记录选项，同时保持旧配置兼容和现有导出逻辑复用。

**Architecture:** 后端继续保留单一 `email_config` 配置表，在现有接口上扩展字段并统一由 `send_overtime_report()` 读取配置。报表范围与调度时间各自抽成内部纯函数，前端邮件页只增加一个轻量配置面板，不引入多任务模型。

**Tech Stack:** Flask, SQLite, 原生 JavaScript, pytest, Node-based frontend script tests.

---

## File Structure

- Modify: `backend/app.py`
  - 扩展 `email_config` 表结构和兼容迁移。
  - 扩展邮件配置 GET/PUT 接口。
  - 新增报表范围解析、下一次调度时间计算、摘要/CSV 组合发送逻辑。
- Modify: `frontend/index.html`
  - 扩展邮件页配置项。
- Modify: `frontend/css/style.css`
  - 为邮件页新增频率和报表内容控件样式。
- Modify: `frontend/js/admin.js`
  - 加载/保存新增邮件配置字段，处理频率切换显示。
- Modify: `管理员使用指南.html`
  - 更新邮件配置和发送选项说明。
- Create: `tests/test_email_config_options.py`
  - 覆盖配置迁移、GET/PUT、默认值。
- Create: `tests/test_email_schedule_logic.py`
  - 覆盖每日/每周/每月/月末调度与报表范围计算。
- Create: `tests/test_email_report_content.py`
  - 覆盖摘要、CSV、摘要+CSV、范围外记录清单、仅有记录成员。
- Modify: `tests/test_frontend_static_assets.py`
  - 断言邮件页新增控件和静态版本号。
- Create: `tests/test_frontend_email_options.py`
  - Node 脚本测试前端邮件配置加载/保存与条件显示。

### Task 1: 扩展邮件配置模型与 API

**Files:**
- Modify: `backend/app.py`
- Create: `tests/test_email_config_options.py`

- [ ] **Step 1: 写失败测试，约束默认字段与 GET 返回结构**

```python
def test_email_config_defaults_include_report_options(monkeypatch, tmp_path):
    app_module = load_module(monkeypatch, tmp_path)
    client = app_module.app.test_client()

    login = client.post("/api/login", json={"username": "admin", "password": "admin123"})
    token = login.get_json()["token"]
    response = client.get("/api/email-config", headers={"Authorization": f"Bearer {token}"})
    config = response.get_json()["config"]

    assert config["schedule_frequency"] == "daily"
    assert config["schedule_weekday"] == 1
    assert config["schedule_month_day"] == "last"
    assert config["report_period"] == "this_month"
    assert config["report_content"] == "summary"
    assert config["member_filter"] == "all"
    assert config["include_out_of_range"] == 1
```

- [ ] **Step 2: 运行测试确认失败**

Run: `python -m pytest tests/test_email_config_options.py::test_email_config_defaults_include_report_options -q`
Expected: FAIL，提示 `schedule_frequency` 等字段不存在或 KeyError。

- [ ] **Step 3: 在后端补齐表字段迁移与默认值**

```python
EMAIL_CONFIG_DEFAULTS = {
    "schedule_frequency": "daily",
    "schedule_weekday": 1,
    "schedule_month_day": "last",
    "report_period": "this_month",
    "report_content": "summary",
    "member_filter": "all",
    "include_out_of_range": 1,
}
```

- [ ] **Step 4: 扩展 GET/PUT 接口支持新字段并校验枚举**

```python
VALID_FREQUENCIES = {"daily", "weekly", "monthly"}
VALID_PERIODS = {"yesterday", "this_week", "last_week", "this_month", "last_month"}
VALID_CONTENTS = {"summary", "csv", "summary_csv"}
VALID_MEMBER_FILTERS = {"all", "with_records"}
```

- [ ] **Step 5: 再补一条 PUT 测试，验证写入与密码保留**

```python
def test_email_config_put_preserves_masked_password_and_saves_options(monkeypatch, tmp_path):
    app_module = load_module(monkeypatch, tmp_path)
    client = app_module.app.test_client()
    token = client.post("/api/login", json={"username": "admin", "password": "admin123"}).get_json()["token"]

    client.put(
        "/api/email-config",
        headers={"Authorization": f"Bearer {token}"},
        json={
            "smtp_host": "smtp.qq.com",
            "smtp_pass": "secret",
            "schedule_frequency": "weekly",
            "schedule_weekday": 5,
            "schedule_month_day": "last",
            "report_period": "last_week",
            "report_content": "summary_csv",
            "member_filter": "with_records",
            "include_out_of_range": 1,
        },
    )
```

- [ ] **Step 6: 运行配置测试，确认通过**

Run: `python -m pytest tests/test_email_config_options.py -q`
Expected: PASS。

- [ ] **Step 7: 提交**

```bash
git add tests/test_email_config_options.py backend/app.py
git commit -m "feat: extend email report config options"
```

### Task 2: 抽离报表范围与调度时间计算

**Files:**
- Modify: `backend/app.py`
- Create: `tests/test_email_schedule_logic.py`

- [ ] **Step 1: 写失败测试，约束报表范围计算**

```python
def test_resolve_report_period_ranges():
    now = datetime.fromisoformat("2026-06-09T15:00:00")

    assert resolve_report_period("yesterday", now) == ("2026-06-08", "2026-06-08")
    assert resolve_report_period("this_week", now) == ("2026-06-08", "2026-06-09")
    assert resolve_report_period("last_week", now) == ("2026-06-01", "2026-06-07")
    assert resolve_report_period("this_month", now) == ("2026-06-01", "2026-06-09")
    assert resolve_report_period("last_month", now) == ("2026-05-01", "2026-05-31")
```

- [ ] **Step 2: 运行测试确认失败**

Run: `python -m pytest tests/test_email_schedule_logic.py::test_resolve_report_period_ranges -q`
Expected: FAIL，提示 `resolve_report_period` 未定义。

- [ ] **Step 3: 写最小实现，提供日期范围函数和下次调度函数**

```python
from calendar import monthrange
```

- [ ] **Step 4: 再写 weekly/monthly 失败测试**

```python
def test_next_schedule_time_supports_weekly_and_monthly():
    now = datetime.fromisoformat("2026-06-09T15:00:00")

    weekly = next_schedule_time({"schedule_frequency": "weekly", "schedule_weekday": 5, "schedule_hour": 9, "schedule_minute": 30}, now)
    monthly = next_schedule_time({"schedule_frequency": "monthly", "schedule_month_day": "last", "schedule_hour": 9, "schedule_minute": 0}, now)

    assert weekly.isoformat() == "2026-06-12T09:30:00"
    assert monthly.isoformat() == "2026-06-30T09:00:00"
```

- [ ] **Step 5: 完成 weekly/monthly 和月末实现**

```python
def month_target_day(year, month, value):
    if str(value) == "last":
        return monthrange(year, month)[1]
    return min(28, max(1, int(value)))
```

- [ ] **Step 6: 运行调度测试，确认通过**

Run: `python -m pytest tests/test_email_schedule_logic.py -q`
Expected: PASS。

- [ ] **Step 7: 提交**

```bash
git add tests/test_email_schedule_logic.py backend/app.py
git commit -m "feat: add email schedule and period logic"
```

### Task 3: 扩展摘要与 CSV 发送内容

**Files:**
- Modify: `backend/app.py`
- Create: `tests/test_email_report_content.py`

- [ ] **Step 1: 写失败测试，约束仅有记录成员与范围外清单**

```python
def test_send_overtime_report_builds_summary_for_selected_period(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        ok, msg = app_module.send_overtime_report()
        assert ok is True
```

- [ ] **Step 2: 运行测试确认失败**

Run: `python -m pytest tests/test_email_report_content.py::test_send_overtime_report_builds_summary_for_selected_period -q`
Expected: FAIL，现有实现只会发送本月摘要且没有范围外清单。

- [ ] **Step 3: 抽出按时间范围取记录和按成员过滤的内部函数**

```python
def get_report_records(from_date, to_date):
    ...


def filter_report_users(users, rows, member_filter):
    ...
```

- [ ] **Step 4: 改造 `send_overtime_report()` 读配置生成标题、摘要、CSV**

```python
def send_overtime_report():
    cfg = normalize_email_config(dict(cfg))
    from_date, to_date = resolve_report_period(cfg["report_period"], now)
```

- [ ] **Step 5: 再补摘要/CSV 组合测试**

```python
def test_send_overtime_report_supports_summary_csv_modes(monkeypatch, tmp_path):
    ...
```

- [ ] **Step 6: 最小化扩展 `send_email()` 支持附件**

```python
def send_email(to, subject, html_body, attachments=None):
    ...
```

- [ ] **Step 7: 运行报表内容测试，确认通过**

Run: `python -m pytest tests/test_email_report_content.py -q`
Expected: PASS。

- [ ] **Step 8: 提交**

```bash
git add tests/test_email_report_content.py backend/app.py
git commit -m "feat: add configurable email report payloads"
```

### Task 4: 扩展邮件页前端配置

**Files:**
- Modify: `frontend/index.html`
- Modify: `frontend/css/style.css`
- Modify: `frontend/js/admin.js`
- Modify: `tests/test_frontend_static_assets.py`
- Create: `tests/test_frontend_email_options.py`

- [ ] **Step 1: 写失败测试，约束邮件页新增控件**

```python
def test_email_page_exposes_schedule_and_report_option_controls():
    index = (ROOT / "frontend" / "index.html").read_text(encoding="utf-8")
```

- [ ] **Step 2: 运行测试确认失败**

Run: `python -m pytest tests/test_frontend_static_assets.py::test_email_page_exposes_schedule_and_report_option_controls -q`
Expected: FAIL。

- [ ] **Step 3: 在 HTML 中加入发送频率、周几、月日期和报表内容控件**

```html
<select class="setting-select" id="email-frequency" onchange="toggleEmailScheduleMode()"></select>
```

- [ ] **Step 4: 在 `admin.js` 增加加载/保存和条件显示逻辑**

```javascript
OT.toggleEmailScheduleMode = function toggleEmailScheduleMode(){
  const frequency = document.getElementById('email-frequency').value;
};
```

- [ ] **Step 5: 写 Node 测试，验证加载/保存参数和条件显示**

```python
def test_frontend_email_options_load_and_save_extended_fields():
    script = r"""
const fs = require('fs');
"""
```

- [ ] **Step 6: 运行前端邮件测试，确认通过**

Run: `python -m pytest tests/test_frontend_static_assets.py::test_email_page_exposes_schedule_and_report_option_controls tests/test_frontend_email_options.py -q`
Expected: PASS。

- [ ] **Step 7: 提交**

```bash
git add frontend/index.html frontend/css/style.css frontend/js/admin.js tests/test_frontend_static_assets.py tests/test_frontend_email_options.py
git commit -m "feat: add email report option controls"
```

### Task 5: 更新立即发送与使用指南并做最终验证

**Files:**
- Modify: `backend/app.py`
- Modify: `管理员使用指南.html`
- Modify: `tests/test_frontend_static_assets.py`

- [ ] **Step 1: 写失败测试，约束“立即发送”走当前配置与文档描述更新**

```python
def test_send_now_uses_current_email_config(monkeypatch, tmp_path):
    with load_app(monkeypatch, tmp_path) as (app_module, _):
        ...
```

- [ ] **Step 2: 运行测试确认失败或文档检查失败**

Run: `python -m pytest tests/test_frontend_static_assets.py -q`
Expected: FAIL，邮件页说明未更新或静态版本未变化。

- [ ] **Step 3: 更新指南文案和前端版本号**

```html
<li><strong>发送频率</strong>：支持每日、每周、每月</li>
```

- [ ] **Step 4: 若前端静态资源变更，升级缓存版本**

```javascript
const CACHE_NAME = 'ot-tracker-v15';
```

- [ ] **Step 5: 运行全量测试与静态检查**

Run: `python -m pytest tests -q`
Expected: PASS。

Run: `node --check frontend/js/utils.js && node --check frontend/js/auth.js && node --check frontend/js/stats.js && node --check frontend/js/records.js && node --check frontend/js/clock.js && node --check frontend/js/admin.js && node --check frontend/js/app.js`
Expected: no output。

Run: `rg -n "加班|åŠ ç­|鍔犂彮" frontend backend README.md 使用指南.html 管理员使用指南.html`
Expected: no matches。

- [ ] **Step 6: 提交**

```bash
git add backend/app.py frontend/index.html frontend/css/style.css frontend/js/admin.js frontend/sw.js 管理员使用指南.html tests
git commit -m "feat: finish configurable email reports"
```