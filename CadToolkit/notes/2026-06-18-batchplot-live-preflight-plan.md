# Batch Plot Live Preflight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refresh the batch plot preflight list when the user changes device or filename settings.

**Architecture:** Keep preflight size and order generation in `BatchPlotCommands.cs`. Move target text regeneration into `BatchPlotDialog`, where the visible device and filename mode are known. The dialog will update the top summary, list target column, and copy text using the current control values.

**Tech Stack:** C# WinForms, PowerShell structural tests.

---

### Task 1: Tests

**Files:**
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Assert `BatchPlotDialog` receives drawing name.
- [ ] Assert `BatchPlotDialog` has `RefreshBatchPlotPreflight`.
- [ ] Assert `cmbDevice.TextChanged` refreshes preflight.
- [ ] Assert `cmbFileNameMode.SelectedIndexChanged` refreshes preflight.
- [ ] Assert dialog summary includes `输出：PDF` and `输出：打印机`.
- [ ] Assert dialog target refresh uses `BuildDialogBatchPlotOutputFileName`.
- [ ] Run `BatchPlot.Tests.ps1` and confirm failure before implementation.

### Task 2: Dialog Refresh Helpers

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`

- [ ] Add constructor parameter `string drawingName`.
- [ ] Store a local `List<BatchPlotPreflightRow>` reference for current rows.
- [ ] Add helper `IsDialogPdfPlotDevice`.
- [ ] Add helper `BuildDialogBatchPlotOutputFileName`.
- [ ] Add helper `GetDialogSelectedFileNameMode`.
- [ ] Add method `RefreshBatchPlotPreflight`.
- [ ] In refresh, update top summary and preflight target subitems.

### Task 3: Wire Events and Command Call

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Wire `cmbDevice.TextChanged` to `RefreshBatchPlotPreflight`.
- [ ] Wire `cmbFileNameMode.SelectedIndexChanged` to `RefreshBatchPlotPreflight`.
- [ ] Pass `drawingName` from `BatchPlotCommands.cs` to the dialog constructor.
- [ ] Keep final plotting loop unchanged.

### Task 4: Verification and Deploy

**Files:**
- All changed batch plot files.

- [ ] Run `BatchPlot.Tests.ps1`.
- [ ] Run `ConfigUpgrade.Tests.ps1`.
- [ ] Run `git diff --check`.
- [ ] Run `deploy-local.ps1`.
