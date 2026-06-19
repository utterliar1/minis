# CadToolkit Batch Plot Preflight Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 同步本机图层规范模板，并让批量打印在严重预检问题存在时先提示确认，减少误打印。

**Architecture:** 配置同步只更新 `LayerStandardWhitelist`、`[LayerStandard]`、`[LayerMap]` 以及内置默认生成文本。批量打印继续复用现有 `BatchPlotPreflightRow.Status`，新增轻量的严重状态识别和确认方法，打印核心不改。

**Tech Stack:** C# WinForms/CAD .NET API、PowerShell 源码断言测试、INI 模板配置。

---

### Task 1: Sync Layer Standard Template

**Files:**
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/CadToolkit.default.ini`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`

- [ ] **Step 1: Write failing tests**
Assert project/default/embedded config contain the local whitelist and standard layer rows.

- [ ] **Step 2: Run test to verify it fails**
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\LayerStandardMatching.Tests.ps1`
Expected: FAIL until template rows match local config.

- [ ] **Step 3: Update templates and embedded default**
Copy only layer-standard-related values from `C:\CadToolkit\CadToolkit.ini`.

- [ ] **Step 4: Re-run test**
Expected: PASS.

### Task 2: Batch Plot Serious Preflight Confirmation

**Files:**
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] **Step 1: Write failing tests**
Assert helper methods exist for severe preflight detection and confirmation, and command checks rows before plotting.

- [ ] **Step 2: Run test to verify it fails**
Run: `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1`
Expected: FAIL until helpers are implemented.

- [ ] **Step 3: Implement minimal code**
Add `HasBatchPlotBlockingPreflightIssue`, `IsBatchPlotBlockingStatus`, and `ConfirmBatchPlotPreflightIssues`; call after dialog OK and refreshed row status.

- [ ] **Step 4: Re-run focused tests**
Run BatchPlot and LayerStandard tests.

### Task 3: Final Validation and Deploy

**Files:**
- No new code files.

- [ ] **Step 1: Run key validation**
Run BatchPlot, LayerStandardMatching, ConfigDiagnostics, ConfigUpgrade tests plus `git diff --check`.

- [ ] **Step 2: Deploy local**
Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\deploy-local.ps1`.
