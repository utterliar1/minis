# Batch Plot Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize CadToolkit batch plot code so shared workflow, layout logic, preflight logic, and CAD-specific plot command adaptation are separated without changing user-visible behavior.

**Architecture:** Keep `CT_BATCHPLOT` as a thin command workflow and move supporting logic into focused partial-class files. Use the existing command-line `-PLOT` implementation as the primary printer path for AutoCAD, ZWCAD, and GstarCAD, while isolating the older Plot API implementation as a legacy fallback.

**Tech Stack:** C# .NET Framework 4.8, AutoCAD/ZWCAD/GstarCAD managed APIs, PowerShell structural tests, existing local deploy script.

---

### Task 1: Add Structure Tests

**Files:**
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Add tests that require new focused files:
  - `BatchPlotModels.cs`
  - `BatchPlotFrameService.cs`
  - `BatchPlotLayoutService.cs`
  - `BatchPlotPreflightService.cs`
  - `BatchPlotHost.cs`
  - `BatchPlotCommandPrinter.cs`
  - `BatchPlotApiPrinter.cs`
- [ ] Add tests that all three CAD project files include each new file.
- [ ] Add tests that command printer helpers use neutral names such as `RunBatchPlotWithPlotCommand`, `BuildPlotCommand`, `ResolvePlotCommandSettings`, `SendPlotCommand`.
- [ ] Add tests that old shared command-path names with `Gstar` are gone from production code.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1` and verify the new tests fail before implementation.

### Task 2: Move Models, Frame, Layout, and Preflight Logic

**Files:**
- Create: `CadToolkit/src/CadToolkit/BatchPlotModels.cs`
- Create: `CadToolkit/src/CadToolkit/BatchPlotFrameService.cs`
- Create: `CadToolkit/src/CadToolkit/BatchPlotLayoutService.cs`
- Create: `CadToolkit/src/CadToolkit/BatchPlotPreflightService.cs`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Move nested model classes to `BatchPlotModels.cs`.
- [ ] Move frame block selection, frame collection, and title block attribute helpers to `BatchPlotFrameService.cs`.
- [ ] Move sort, margin, paper-size, scale, output path, and drawing-name helpers to `BatchPlotLayoutService.cs`.
- [ ] Move preflight row building, status, duplicate detection, and confirmation helpers to `BatchPlotPreflightService.cs`.
- [ ] Keep `BatchPlotCommands.cs` focused on the command workflow.
- [ ] Run `BatchPlot.Tests.ps1`.

### Task 3: Create Host and Command Printer Adapter

**Files:**
- Create: `CadToolkit/src/CadToolkit/BatchPlotHost.cs`
- Create: `CadToolkit/src/CadToolkit/BatchPlotCommandPrinter.cs`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Move CAD namespace helpers, reflection helpers, device/media normalization, and optional invocation helpers to `BatchPlotHost.cs`.
- [ ] Move command-line printer logic to `BatchPlotCommandPrinter.cs`.
- [ ] Rename shared command-path helpers:
  - `RunGstarBatchPlotWithPlotCommand` -> `RunBatchPlotWithPlotCommand`
  - `PlotFrameToPdfWithPlotCommand` -> `PlotFrameToPdfWithPlotCommand`
  - `PlotFrameToDeviceWithPlotCommand` -> `PlotFrameToDeviceWithPlotCommand`
  - `BuildGstarPlotCommand` -> `BuildPlotCommand`
  - `LogGstarPlotGeometry` -> `LogPlotCommandGeometry`
  - `ResolveGstarPlotCommandSettings` -> `ResolvePlotCommandSettings`
  - `ResolveGstarPlotCommandDeviceName` -> `ResolvePlotCommandDeviceName`
  - `ResolveGstarPlotCommandPaperName` -> `ResolvePlotCommandPaperName`
  - `SendGstarPlotCommand` -> `SendPlotCommand`
  - `BuildGstarPlotScaleInput` -> `BuildPlotCommandScaleInput`
- [ ] Keep AutoCAD COM `SendCommand` behavior intact.
- [ ] Run `BatchPlot.Tests.ps1`.

### Task 4: Isolate Legacy Plot API Printer

**Files:**
- Create: `CadToolkit/src/CadToolkit/BatchPlotApiPrinter.cs`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Move `BatchPlotApi` and related API-only helpers to `BatchPlotApiPrinter.cs`.
- [ ] Keep API implementation compile-safe but not on the primary AutoCAD/ZWCAD/GstarCAD command path.
- [ ] Run `BatchPlot.Tests.ps1`.

### Task 5: Wire Project Files and Verify

**Files:**
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.ZWCAD.csproj`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.GstarCAD.csproj`
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Add each new C# file to all three CAD project files.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\deploy-local.ps1`.
- [ ] Do not commit until tests and deployment build pass.

### Self-Review

- Spec coverage: Covers file split, neutral naming, three-CAD command adapter, legacy API isolation, tests, and deploy verification.
- Placeholder scan: No TBD/TODO placeholders.
- Scope: Refactor only; no behavior changes intended.
