# CadToolkit Standard Entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把主面板底部重复的“配置体检/规范中心”入口收口为一个齿轮规范中心入口。

**Architecture:** `PanelBuilder` 只负责返回统一面板动作；齿轮按钮改为 `STANDARDCENTER`，删除单独“规”按钮。`StandardCenterCommands` 保留命令分发，但窗口内部用“规范工具”和“配置工具”两个视觉分组表达层级。

**Tech Stack:** C# WinForms、AutoCAD/ZWCAD/GstarCAD 命令分发、PowerShell 源码断言测试。

---

### Task 1: 面板入口收口

**Files:**
- Modify: `CadToolkit/tests/StandardCenter.Tests.ps1`
- Modify: `CadToolkit/tests/ConfigDiagnostics.Tests.ps1`
- Modify: `CadToolkit/src/CadToolkit.UI/PanelBuilder.cs`
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`

- [ ] **Step 1: Write failing tests**

Update StandardCenter tests to assert the gear button is the standard center entry and the separate `btnStandardCenter` no longer exists.

- [ ] **Step 2: Run tests and confirm failure**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\StandardCenter.Tests.ps1`
Expected: FAIL because current code still has a separate `btnStandardCenter` and gear returns `CONFIGCHECK`.

- [ ] **Step 3: Implement minimal code**

Change `btnConfigCheck` into the single standard center gear entry. Remove `btnStandardCenter` creation, bar add, resize and initial placement. Keep `CONFIGCHECK` command available through Standard Center and command line.

- [ ] **Step 4: Run tests and confirm pass**

Run StandardCenter and ConfigDiagnostics tests.

### Task 2: 规范中心分组

**Files:**
- Modify: `CadToolkit/src/CadToolkit/StandardCenterCommands.cs`
- Modify: `CadToolkit/tests/StandardCenter.Tests.ps1`

- [ ] **Step 1: Write failing tests**

Assert Standard Center contains `规范工具` and `配置工具` labels.

- [ ] **Step 2: Run tests and confirm failure**

Run StandardCenter tests. Expected: FAIL until labels exist.

- [ ] **Step 3: Implement minimal UI grouping**

Add two small section labels and place action buttons under them without changing command names.

- [ ] **Step 4: Run full focused validation**

Run StandardCenter, ConfigDiagnostics, ConfigMaintenance, and ConfigUpgrade tests plus `git diff --check`.
