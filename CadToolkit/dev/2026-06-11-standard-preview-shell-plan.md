# CadToolkit Standard Preview Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract a lightweight reusable preview shell for CadToolkit standardization dialogs while preserving current layer and text style behavior.

**Architecture:** Add one focused WinForms helper file for shared preview form, filter controls, tree helpers, and action buttons. Keep layer and text style business logic in their existing command files, with only narrow calls into the helper.

**Tech Stack:** C# .NET Framework 4.8, WinForms, AutoCAD-compatible command assembly, PowerShell test scripts.

---

### Task 1: Add Red Tests For Shared Helper

**Files:**
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`
- Modify: `CadToolkit/tests/TextStyleStandard.Tests.ps1`

- [ ] **Step 1: Write failing source-structure tests**

Add assertions that require `StandardPreviewUi.cs`, shared filter controls, shared tree report formatting, and shared search filtering to exist. Also require `LayerCommands.cs` and `TextStyleCommands.cs` to call the shared helper.

- [ ] **Step 2: Run related tests to verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\TextStyleStandard.Tests.ps1
```

Expected: fail because `StandardPreviewUi.cs` and helper calls do not exist yet.

### Task 2: Create Standard Preview Helper

**Files:**
- Create: `CadToolkit/src/CadToolkit/StandardPreviewUi.cs`

- [ ] **Step 1: Add helper types and methods**

Create an internal static helper in namespace `CadToolkit` with:

```csharp
internal sealed class StandardPreviewFilterControls
{
    public RadioButton All;
    public RadioButton Unknown;
    public RadioButton Migration;
    public RadioButton Whitelist;
    public Label SearchLabel;
    public TextBox Search;
}
```

Add methods:

```csharp
static Form CreateStandardPreviewForm(string title)
static StandardPreviewFilterControls CreateStandardPreviewFilterControls(string allText, string unknownText, string migrationText, string whitelistText, string searchText)
static TreeView CreateStandardPreviewTree(int height)
static Button CreateStandardPreviewButton(string text, int left, DialogResult dialogResult)
static TreeNode[] FilterStandardPreviewNodes(TreeNode[] filtered, string searchText)
static string FormatStandardPreviewTreeReport(TreeNode[] nodes)
static void UpdateStandardPreviewTree(TreeView tree, TreeNode[] nodes, bool expand)
```

- [ ] **Step 2: Run related tests**

Expected: helper existence tests pass, command call tests still fail until callers are migrated.

### Task 3: Migrate Layer Preview To Helper

**Files:**
- Modify: `CadToolkit/src/CadToolkit/LayerCommands.cs`
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`

- [ ] **Step 1: Replace duplicated layer UI creation**

Use helper methods for the form, filter controls, tree, and buttons. Preserve current positions, sizes, labels, and control names where behavior depends on local variables.

- [ ] **Step 2: Replace duplicated layer tree utilities**

Change `BuildSearchedLayerPlanTreeNodes`, `FormatLayerPlanTreeReport`, and `BuildLayerPlanTreePreview` to delegate shared filtering, report formatting, and tree update behavior to `StandardPreviewUi`.

- [ ] **Step 3: Run layer tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: pass.

### Task 4: Migrate Text Style Preview To Helper

**Files:**
- Modify: `CadToolkit/src/CadToolkit/TextStyleCommands.cs`

- [ ] **Step 1: Replace duplicated text style UI creation**

Use helper methods for the form, filter controls, tree, and buttons. Preserve text style-specific option controls and refresh logic in `TextStyleCommands.cs`.

- [ ] **Step 2: Replace duplicated text style tree utilities**

Change `BuildSearchedTextStylePlanTreeNodes`, `FormatTextStylePlanTreeReport`, and `BuildTextStylePlanTreePreview` to delegate shared filtering, report formatting, and tree update behavior to `StandardPreviewUi`.

- [ ] **Step 3: Run text style tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\TextStyleStandard.Tests.ps1
```

Expected: pass.

### Task 5: Verify And Deploy

**Files:**
- No source edits expected.

- [ ] **Step 1: Run full tests and diff check**

Run:

```powershell
git diff --check
$tests = Get-ChildItem CadToolkit\tests -Filter *.Tests.ps1 | Sort-Object Name
foreach ($test in $tests) {
  powershell -NoProfile -ExecutionPolicy Bypass -File $test.FullName
  if ($LASTEXITCODE -ne 0) { throw "Test failed: $($test.Name)" }
}
```

Expected: all tests exit 0; existing environment warnings may appear.

- [ ] **Step 2: Check CAD processes**

Run:

```powershell
Get-Process | Where-Object { $_.ProcessName -match 'acad|zwcad|gcad|gstar' }
```

Expected: no running CAD process before local deploy.

- [ ] **Step 3: Deploy locally**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\deploy-local.ps1
```

Expected: AutoCAD, ZWCAD, and GstarCAD build and copy outputs to `C:\CadToolkit`; existing `CadToolkit.ini` is preserved.

