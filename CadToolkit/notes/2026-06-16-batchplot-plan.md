# CadToolkit Batch Plot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a first-version `CT_BATCHPLOT` command that batch exports manually selected model-space frames to individual PDF files.

**Architecture:** Keep batch plotting isolated in a new `BatchPlotCommands.cs` partial command file and a small `BatchPlotDialog` in the UI project. Persist the last-used device, paper, plot style, centering, and auto-rotation settings through the existing root-level `CadToolkit.ini` mechanism.

**Tech Stack:** C# .NET Framework 4.8, WinForms, CAD runtime APIs accessed through existing conditional namespaces plus reflection for Plot API compatibility, PowerShell regex/compile tests.

---

### Task 1: Configuration, Command Registration, And Tests

**Files:**
- Create: `CadToolkit/tests/BatchPlot.Tests.ps1`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/CadToolkit.default.ini`

- [ ] **Step 1: Write the failing test**

Create `BatchPlot.Tests.ps1` asserting:
- root settings exist: `BatchPlotDevice`, `BatchPlotPaper`, `BatchPlotStyle`, `BatchPlotAutoRotate`, `BatchPlotCenter`
- `Config` exposes matching properties with setters where the dialog needs to save values
- official command list includes `批量打印=CT_BATCHPLOT`
- diagnostics auto-repair knows the new root settings and command

- [ ] **Step 2: Run test to verify it fails**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit/tests/BatchPlot.Tests.ps1`
Expected: FAIL because batch plot config and command do not exist.

- [ ] **Step 3: Implement minimal configuration**

Add default root settings and properties:
- `BatchPlotDevice=DWG To PDF.pc3`
- `BatchPlotPaper=A3`
- `BatchPlotStyle=monochrome.ctb`
- `BatchPlotAutoRotate=true`
- `BatchPlotCenter=true`

Add `批量打印=CT_BATCHPLOT` after `快速标注=CT_QUICKDIM`.

- [ ] **Step 4: Run test to verify it passes**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit/tests/BatchPlot.Tests.ps1`
Expected: PASS.

### Task 2: Dialog And Project References

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Extend the failing test**

Add assertions that `BatchPlotDialog` exists, has Chinese labels, loads from `Config.BatchPlot*`, and saves settings on OK.

- [ ] **Step 2: Run test to verify it fails**

Run the batch plot test. Expected: FAIL because the dialog does not exist.

- [ ] **Step 3: Implement the dialog**

Create a compact WinForms dialog with text/combobox fields for PDF device, paper, plot style and checkboxes for auto-rotate and center. Save values back to `Config` when OK is clicked.

- [ ] **Step 4: Run test to verify it passes**

Run the batch plot test. Expected: PASS.

### Task 3: Command Skeleton, Sorting, Naming, And Three-End References

**Files:**
- Create: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.ZWCAD.csproj`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.GstarCAD.csproj`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Extend the failing test**

Assert:
- command is registered as `[CommandMethod("CT_BATCHPLOT", CommandFlags.UsePickSet)]`
- command reads pickfirst/current selection
- helper sorts frames from top-to-bottom then left-to-right
- helper builds `DWG名-001.pdf`
- all three CAD projects compile `BatchPlotCommands.cs`

- [ ] **Step 2: Run test to verify it fails**

Run the batch plot test. Expected: FAIL because the command file does not exist.

- [ ] **Step 3: Implement command skeleton and helpers**

Add `BatchPlotCommands.cs` with:
- `BatchPlot()`
- `BatchPlotSettings`
- `BatchPlotFrame`
- `CollectPlotFrames`
- `SortPlotFrames`
- `BuildBatchPlotOutputPath`

- [ ] **Step 4: Run test to verify it passes**

Run the batch plot test. Expected: PASS.

### Task 4: Plot API Execution

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Extend the failing test**

Assert `BatchPlotCommands.cs` contains:
- `PlotSettings`
- `PlotInfo`
- `PlotFactory`
- `SetPlotWindowArea`
- `SetStdScaleType`
- `SetPlotCentered`
- `SetCurrentStyleSheet`
- `BeginDocument`

- [ ] **Step 2: Run test to verify it fails**

Run the batch plot test. Expected: FAIL because plot execution is not implemented.

- [ ] **Step 3: Implement reflection-based Plot API wrapper**

Use runtime type lookup for each platform namespace so the source still compiles with local stubs and can call real CAD Plot APIs at runtime.

- [ ] **Step 4: Run test to verify it passes**

Run the batch plot test. Expected: PASS.

### Task 5: Documentation, Full Verification, And Local Deploy

**Files:**
- Modify: `CadToolkit/README.md`
- Modify: `CadToolkit/CadToolkit使用手册.html`

- [ ] **Step 1: Extend the failing test**

Assert README and manual document `批量打印` and `CT_BATCHPLOT`.

- [ ] **Step 2: Run test to verify it fails**

Run the batch plot test. Expected: FAIL before docs are updated.

- [ ] **Step 3: Update docs**

Add concise usage notes: select frames first, run command, one PDF per frame, output to DWG directory, remembers settings.

- [ ] **Step 4: Run focused tests**

Run `BatchPlot.Tests.ps1`, `ConfigDiagnostics.Tests.ps1`, and `DeploymentConfig.Tests.ps1`.

- [ ] **Step 5: Run all tests**

Run all `CadToolkit/tests/*.Tests.ps1` serially.

- [ ] **Step 6: Deploy locally**

Run `CadToolkit/deploy-local.ps1` to update `C:\CadToolkit`.

### Task 6: Installed Printer Dropdown And GstarCAD Plot Compatibility

**Goal:** Let the batch plot dialog list installed Windows printers, keep PDF output for PDF/PC3 devices, send physical printers directly to the device, and stop assuming GstarCAD exposes plot types in the same namespace as AutoCAD.

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Extend tests first**

Assert the dialog uses `PrinterSettings.InstalledPrinters`, keeps `DWG To PDF.pc3`, and saves the selected device. Assert the command has `IsPdfPlotDevice`, separates PDF paths from printer paths, logs device/paper/style details, and resolves GstarCAD plot types from fallback names.

- [ ] **Step 2: Verify the tests fail**

Run `powershell -ExecutionPolicy Bypass -File CadToolkit/tests/BatchPlot.Tests.ps1`. Expected: FAIL before implementation.

- [ ] **Step 3: Implement dropdown and output mode**

Use `System.Drawing.Printing.PrinterSettings.InstalledPrinters` in `BatchPlotDialog`, sorted after the CAD PDF device. In the command, if the device name looks like PDF/PC3/XPS, create one PDF path per frame; otherwise pass `null` as the output file so CAD sends the job to the selected printer.

- [ ] **Step 4: Implement GstarCAD plot type fallback**

Resolve plot types with a candidate list. For GstarCAD, try the normal `GrxCAD.*` names first, then fallback type names discovered from local GstarCAD binaries such as `GcPlPlotInfo`, `GcPlPlotInfoValidator`, and `GcPlPlotFactory`.

- [ ] **Step 5: Verify and deploy**

Run focused tests, all CadToolkit tests, then `deploy-local.ps1`.
