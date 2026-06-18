# Batch Plot Frame Block Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change batch plot so the user picks one title block as the template, then window-selects a scope and plots only matching frame blocks.

**Architecture:** Keep plotting, config, margins, filenames, and device handling unchanged. Add a narrow selection layer in `BatchPlotCommands.cs` that resolves a block key from the template block, collects `INSERT` candidates from the selected scope, filters by normal or dynamic block definition, then reuses the existing frame collection and sorting. Update `BatchPlotDialog` to show the frame block name and a cleaner summary.

**Tech Stack:** C# WinForms, AutoCAD/GstarCAD/ZWCAD .NET APIs, PowerShell structural tests.

---

### Task 1: Selection Flow Tests

**Files:**
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] Add structural tests that require `CT_BATCHPLOT` to prompt for a template block before collecting frames.
- [ ] Add structural tests for `PromptEntityOptions`, `AddAllowedClass(typeof(BlockReference), true)`, and an `INSERT` `SelectionFilter`.
- [ ] Add structural tests for helpers named `GetBatchPlotFrameBlockKey`, `CollectBatchPlotFrameBlockIds`, and `IsBatchPlotSameFrameBlock`.
- [ ] Add structural tests that `BatchPlotDialog` receives a frame block name.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1` and verify the new tests fail before implementation.

### Task 2: Template Block and Scope Selection

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Replace the current generic `GetSelectionOrAbort("\n未选择图框对象。")` entry with a template block prompt.
- [ ] Use `PromptEntityOptions("\n选择一个图框块作为模板：")`.
- [ ] Restrict the first pick with `AddAllowedClass(typeof(BlockReference), true)`.
- [ ] Resolve the template block key with `GetBatchPlotFrameBlockKey(ObjectId blockReferenceId, Transaction tr)`.
- [ ] Prompt for scope selection with `PromptSelectionOptions.MessageForAdding = "\n框选要打印的范围："`.
- [ ] Use `SelectionFilter` with `TypedValue(0, "INSERT")`.
- [ ] Filter selected block references with `IsBatchPlotSameFrameBlock`.
- [ ] Keep cancellation quiet and show `未在选择范围内找到同名图框块。` when the scope contains no matching blocks.

### Task 3: Dynamic Block Matching and Frame Collection

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Implement `GetBatchPlotFrameBlockKey` so normal blocks use `BlockTableRecord.Name`.
- [ ] For dynamic blocks, prefer `BlockReference.DynamicBlockTableRecord` when it is valid and not null.
- [ ] Fall back to `BlockReference.BlockTableRecord` if dynamic lookup fails.
- [ ] Include a display name in the key for dialog text.
- [ ] Reuse `CollectPlotFrames(ObjectId[] selectedIds)` after filtering so extents, skip logging, and sorting stay unchanged.
- [ ] Run `BatchPlot.Tests.ps1` and verify selection tests pass.

### Task 4: Dialog Summary Polish

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`

- [ ] Change the dialog constructor to `BatchPlotDialog(int frameCount, string frameBlockName, string outputDirectory)`.
- [ ] Replace the old repeated summary with concise top text that includes the frame block name and count.
- [ ] Keep existing controls and saved settings unchanged.
- [ ] Update the command call to pass the selected frame block display name.
- [ ] Run `BatchPlot.Tests.ps1`.

### Task 5: Verification and Local Deploy

**Files:**
- All changed batch plot files.

- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\ConfigUpgrade.Tests.ps1`.
- [ ] Run `git diff --check`.
- [ ] Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\deploy-local.ps1`.
- [ ] Report changed files, verification output, and local deploy result.
