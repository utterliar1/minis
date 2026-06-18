# Batch Plot Usability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make batch plotting easier to trust by using millimeter page margins, selectable output filename formats, and a clearer pre-print summary.

**Architecture:** Keep the existing `BatchPlotDialog` and `BatchPlotCommands` structure. Add new persistent config keys for millimeter margin and filename format while keeping the old percentage key harmless for compatibility. Convert millimeter margins into plot-window expansion using configured paper size and orientation before plotting.

**Tech Stack:** C# WinForms, reflection-based CAD APIs, PowerShell structural tests.

---

### Task 1: Config and Dialog

**Files:**
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/CadToolkit.default.ini`
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Add tests for `BatchPlotMarginMm=5`, `BatchPlotFileNameMode=DrawingDashIndex`, dialog fields, page margin label in mm, filename mode combo, and summary text.
- [ ] Implement config defaults and typed properties.
- [ ] Update dialog to expose `MarginMm` and `FileNameMode`, save them to config, and show a concise summary.
- [ ] Run `BatchPlot.Tests.ps1`.

### Task 2: Plot Behavior

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Add tests for `MarginMm`, `BuildBatchPlotOutputPath(... fileNameMode ...)`, paper-size helper, and millimeter margin expansion.
- [ ] Implement filename modes: `DrawingDashIndex`, `DrawingUnderscoreIndex`, `IndexOnly`.
- [ ] Replace percentage expansion with millimeter expansion based on paper size and orientation.
- [ ] Run `BatchPlot.Tests.ps1`.

### Task 3: Verification and Deploy

**Files:**
- All changed batch plot files.

- [ ] Run `BatchPlot.Tests.ps1`.
- [ ] Run `ConfigUpgrade.Tests.ps1`.
- [ ] Run `git diff --check`.
- [ ] Run `deploy-local.ps1`.
