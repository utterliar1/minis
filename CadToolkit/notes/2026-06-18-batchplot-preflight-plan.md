# Batch Plot Preflight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a preflight list to batch plot and restrict plot style choices to `.ctb` styles.

**Architecture:** Keep plotting behavior inside `BatchPlotCommands.cs` and UI rendering inside `Dialogs.cs`. Add a small preflight row model in the UI assembly so the command can build rows after sorting frames and before showing the dialog. The dialog displays rows, copy text, warning text, and still returns the same settings used by the plotting loop.

**Tech Stack:** C# WinForms, AutoCAD/GstarCAD/ZWCAD .NET APIs, PowerShell structural tests.

---

### Task 1: Add Structural Tests

**Files:**
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Add assertions for `BatchPlotPreflightRow`.
- [ ] Add assertions for `BuildBatchPlotPreflightRows`.
- [ ] Add assertions that PDF rows call `BuildBatchPlotOutputPath`.
- [ ] Add assertions that printer rows use `发送到打印机`.
- [ ] Add assertions that `BatchPlotDialog` constructor accepts `List<BatchPlotPreflightRow>`.
- [ ] Add assertions that dialog uses `ListView`.
- [ ] Add assertions that dialog has `复制预检`.
- [ ] Add assertions that dialog shows `检测到图框尺寸不一致`.
- [ ] Add assertions that plot style helper keeps `.ctb` and excludes `.stb`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1`.
- [ ] Confirm the test fails because the preflight feature is missing.

### Task 2: Add UI Preflight Model and Dialog

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`

- [ ] Add public class `BatchPlotPreflightRow` with string fields `Index`, `Size`, `Orientation`, and `Target`.
- [ ] Change `BatchPlotDialog` constructor to accept `List<BatchPlotPreflightRow> preflightRows` before `outputDirectory`.
- [ ] Add a `ListView` below existing settings controls with columns `序号`, `尺寸`, `方向`, and `目标`.
- [ ] Populate the `ListView` from `preflightRows`.
- [ ] Add `复制预检` button that copies formatted row text to clipboard.
- [ ] Add a warning label containing `检测到图框尺寸不一致，请确认是否混选。`.
- [ ] Show warning only when frame sizes differ more than 3%.
- [ ] Keep OK and Cancel behavior unchanged.

### Task 3: Build Preflight Rows in Command

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Add `BuildBatchPlotPreflightRows(List<BatchPlotFrame> frames, string outputDirectory, string drawingName, string fileNameMode, string deviceName)`.
- [ ] For PDF devices, set target to `Path.GetFileName(BuildBatchPlotOutputPath(...))`.
- [ ] For non-file devices, set target to `发送到打印机`.
- [ ] Set orientation from frame width and height as `横向` or `纵向`.
- [ ] Format size as rounded width x height using invariant culture.
- [ ] Build preflight rows after sorting frames and before opening the dialog.
- [ ] Pass preflight rows into `BatchPlotDialog`.
- [ ] Keep plotting loop unchanged.

### Task 4: CTB-Only Plot Styles

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`

- [ ] Add helper `IsCtbPlotStyleName`.
- [ ] Make `AddPlotStyleName` ignore non-`.ctb` names.
- [ ] Keep configured `BatchPlotStyle` only if it ends with `.ctb`.
- [ ] Ensure fallback styles are still available.
- [ ] Ensure `.stb` names returned by CAD API are ignored.

### Task 5: Verification and Deploy

**Files:**
- All changed batch plot files.

- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\ConfigUpgrade.Tests.ps1`.
- [ ] Run `git diff --check`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\deploy-local.ps1`.
- [ ] Report verification output and local DLL timestamps.
