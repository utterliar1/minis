# Batch Plot Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve CadToolkit batch plotting so GstarCAD device flows stay stable, plot styles are easier to choose, and plotted frames keep a small configurable paper margin.

**Architecture:** Keep the current batch plot feature structure. Put persistent options in `CadToolkit.Core.Config`, UI controls and style discovery in `CadToolkit.UI.BatchPlotDialog`, and plotting behavior in `CadToolkit.BatchPlotCommands`. Use a small helper to expand frame extents before both GstarCAD command-line plotting and API plotting.

**Tech Stack:** C# 7.3 style WinForms, AutoCAD/GstarCAD/ZWCAD conditional compilation, PowerShell structural tests.

---

## File Map

- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
  - Add `BatchPlotMarginPercent` default, template output line, and typed property.
- Modify: `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`
  - Add `BatchPlotMarginPercent` to required config keys so config diagnosis and repair can restore it.
- Modify: `CadToolkit/CadToolkit.ini`
  - Add the commented default key in the existing batch plot config block.
- Modify: `CadToolkit/CadToolkit.default.ini`
  - Add the same commented default key in the same order.
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
  - Add margin input to `BatchPlotDialog`.
  - Replace hard-coded style list with a helper that discovers `.ctb` and `.stb` files and falls back to common styles.
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
  - Add `MarginPercent` to `BatchPlotSettings`.
  - Expand frame extents before command construction and API plot window creation.
  - Keep `DWG To PDF.pc3` and system-printer GstarCAD command answers unchanged except for using the expanded frame.
- Modify: `CadToolkit/tests/BatchPlot.Tests.ps1`
  - Add structural coverage for config, margin helper usage, style discovery fallback, and the two GstarCAD command flows.

---

### Task 1: Config Default and Diagnostics

**Files:**
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/CadToolkit.default.ini`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Add failing structural tests for the margin config**

Add `BatchPlotMarginPercent=2` to the existing batch plot setting loop in `CadToolkit/tests/BatchPlot.Tests.ps1`:

```powershell
foreach ($setting in @(
    'BatchPlotDevice=DWG To PDF.pc3',
    'BatchPlotPaper=A3',
    'BatchPlotStyle=monochrome.ctb',
    'BatchPlotAutoRotate=true',
    'BatchPlotCenter=true',
    'BatchPlotMarginPercent=2'
)) {
    Assert-Literal "project config contains $setting" $projectConfig $setting
    Assert-Literal "default config contains $setting" $defaultConfig $setting
    $key = $setting.Split('=')[0]
    Assert-Match "embedded default contains $key" $config "$([regex]::Escape($key))"
    Assert-Match "diagnostics knows $key" $diagnostics "$([regex]::Escape($key))"
}
```

Add this property assertion after the existing batch plot config property assertions:

```powershell
Assert-Match 'config exposes batch plot margin property' $config 'public\s+static\s+double\s+BatchPlotMarginPercent'
```

- [ ] **Step 2: Run the test and verify it fails on missing margin config**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: FAIL mentioning `BatchPlotMarginPercent`.

- [ ] **Step 3: Add the config key in the existing batch plot block**

In `CadToolkit/src/CadToolkit.Core/Config.cs`, add the default pair immediately after `BatchPlotCenter`:

```csharp
new KeyValuePair<string, string>("BatchPlotCenter", "true"),
new KeyValuePair<string, string>("BatchPlotMarginPercent", "2")
```

In `BuildDefaultContent`, add the comment and value immediately after `BatchPlotCenter`:

```csharp
sb.AppendLine("# BatchPlotMarginPercent\uFF1A\u6279\u91CF\u6253\u5370\u7559\u767D\u767E\u5206\u6BD4\uFF0C\u9ED8\u8BA4 2\uFF0C0 \u8868\u793A\u4E0D\u989D\u5916\u7559\u767D\u3002");
sb.AppendLine("BatchPlotMarginPercent=2");
```

Add the typed property after `BatchPlotCenter`:

```csharp
public static double BatchPlotMarginPercent { get { return GetDouble("BatchPlotMarginPercent", 2); } set { SaveDouble("BatchPlotMarginPercent", value); } }
```

In `CadToolkit/src/CadToolkit.Core/ConfigDiagnostics.cs`, add the required key after `BatchPlotCenter`:

```csharp
new KeyValuePair<string, string>("BatchPlotCenter", "true"),
new KeyValuePair<string, string>("BatchPlotMarginPercent", "2")
```

In both ini files, add:

```ini
# BatchPlotMarginPercent：批量打印留白百分比，默认 2，0 表示不额外留白。
BatchPlotMarginPercent=2
```

- [ ] **Step 4: Run the test and verify config coverage passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: PASS for the newly added config assertions. Later tests may still fail until subsequent tasks are complete.

---

### Task 2: Margin Helper and Plot Window Usage

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Add failing tests for margin helper and usage**

Add these assertions near the frame and window-area assertions in `CadToolkit/tests/BatchPlot.Tests.ps1`:

```powershell
Assert-Match 'batch plot settings carries margin percent' $batchPlotCommands 'public\s+double\s+MarginPercent'
Assert-Match 'batch plot reads margin percent from dialog' $batchPlotCommands 'settings\.MarginPercent\s*=\s*dlg\.MarginPercent'
Assert-Match 'batch plot expands frame helper exists' $batchPlotCommands 'static\s+BatchPlotFrame\s+ExpandBatchPlotFrame'
Assert-Match 'batch plot clamps negative margin to zero' $batchPlotCommands 'Math\.Max\(0'
Assert-Match 'gstarcad plot command uses expanded frame' $batchPlotCommands 'BatchPlotFrame\s+plotFrame\s*=\s*ExpandBatchPlotFrame\(frame,\s*settings\.MarginPercent\)'
Assert-Match 'gstarcad plot lower left uses expanded frame' $batchPlotCommands 'FormatGstarPlotPoint\(plotFrame\.MinX,\s*plotFrame\.MinY\)'
Assert-Match 'gstarcad plot upper right uses expanded frame' $batchPlotCommands 'FormatGstarPlotPoint\(plotFrame\.MaxX,\s*plotFrame\.MaxY\)'
Assert-Match 'api plot window uses expanded frame' $batchPlotCommands 'CreateExtents2d\(ExpandBatchPlotFrame\(frame,\s*settings\.MarginPercent\)\)'
```

- [ ] **Step 2: Run the test and verify it fails on missing helper**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: FAIL mentioning margin helper or margin property.

- [ ] **Step 3: Add margin to settings and read it from the dialog**

In `BatchPlotSettings`, add:

```csharp
public double MarginPercent;
```

When settings are created in `BatchPlot()`, add:

```csharp
settings.MarginPercent = dlg.MarginPercent;
```

In `ResolveGstarPlotCommandSettings`, copy the value:

```csharp
resolved.MarginPercent = settings.MarginPercent;
```

In `DescribeBatchPlotSettings`, include the value in the diagnostic string:

```csharp
+ "; MarginPercent=" + settings.MarginPercent.ToString(CultureInfo.InvariantCulture)
```

If `CultureInfo` is not already imported in `BatchPlotCommands.cs`, add:

```csharp
using System.Globalization;
```

- [ ] **Step 4: Add the expansion helper**

Add this helper near `SortPlotFrames`:

```csharp
static BatchPlotFrame ExpandBatchPlotFrame(BatchPlotFrame frame, double marginPercent)
{
    double margin = Math.Max(0, marginPercent) / 100.0;
    if (margin <= 0 || frame == null) return frame;

    double dx = frame.Width * margin / 2.0;
    double dy = frame.Height * margin / 2.0;
    var expanded = new BatchPlotFrame();
    expanded.Id = frame.Id;
    expanded.MinX = frame.MinX - dx;
    expanded.MinY = frame.MinY - dy;
    expanded.MaxX = frame.MaxX + dx;
    expanded.MaxY = frame.MaxY + dy;
    return expanded;
}
```

- [ ] **Step 5: Use the expanded frame in GstarCAD command construction**

At the start of `BuildGstarPlotCommand`, add:

```csharp
BatchPlotFrame plotFrame = ExpandBatchPlotFrame(frame, settings.MarginPercent);
```

Replace frame references used by command inputs:

```csharp
inputs.Add(QuoteGstarLispString(GetGstarPlotOrientationInput(plotFrame, settings)));
inputs.Add(QuoteGstarLispString(FormatGstarPlotPoint(plotFrame.MinX, plotFrame.MinY)));
inputs.Add(QuoteGstarLispString(FormatGstarPlotPoint(plotFrame.MaxX, plotFrame.MaxY)));
```

- [ ] **Step 6: Use the expanded frame in API plotting**

In `BatchPlotApi.PlotFrame`, replace the plot window call:

```csharp
Invoke(validator, "SetPlotWindowArea", plotSettings, CreateExtents2d(ExpandBatchPlotFrame(frame, settings.MarginPercent)));
```

Keep rotation based on the original `frame` unless testing shows a need to change it; the margin preserves the same aspect ratio.

- [ ] **Step 7: Run the test and verify margin usage passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: PASS for margin helper and usage assertions.

---

### Task 3: Dialog Margin Input and Style Discovery

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Add failing tests for dialog margin and style discovery**

Add these assertions near the batch plot dialog assertions:

```powershell
Assert-Match 'batch plot dialog exposes margin percent' $dialogs 'public\s+double\s+MarginPercent'
Assert-Match 'batch plot dialog loads margin from config' $dialogs 'MarginPercent\s*=\s*Config\.BatchPlotMarginPercent'
Assert-Match 'batch plot dialog saves margin to config' $dialogs 'Config\.BatchPlotMarginPercent\s*=\s*MarginPercent'
Assert-Match 'batch plot dialog has margin label' $dialogs '\\u7559\\u767D'
Assert-Match 'batch plot dialog parses margin text as double' $dialogs 'double\.TryParse\(txtMargin\.Text\.Trim\(\)'
Assert-Match 'batch plot dialog discovers plot styles helper' $dialogs 'static\s+List<string>\s+GetPlotStyleNames'
Assert-Match 'batch plot dialog scans ctb files' $dialogs '\\*\.ctb'
Assert-Match 'batch plot dialog scans stb files' $dialogs '\\*\.stb'
Assert-Match 'batch plot dialog includes grayscale fallback style' $dialogs 'grayscale\.ctb'
Assert-Match 'batch plot dialog keeps style dropdown editable' $dialogs 'cmbStyle\.DropDownStyle\s*=\s*ComboBoxStyle\.DropDown'
```

- [ ] **Step 2: Run the test and verify it fails on dialog changes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: FAIL mentioning dialog margin or style discovery.

- [ ] **Step 3: Add `MarginPercent` to `BatchPlotDialog`**

Add a public field:

```csharp
public double MarginPercent;
```

Initialize it with:

```csharp
MarginPercent = Config.BatchPlotMarginPercent;
```

Increase dialog height enough for one extra row:

```csharp
ClientSize = new Size(430, 292);
```

Add a margin label and text box below the style row:

```csharp
var lblMargin = new Label();
lblMargin.Text = "\u7559\u767D\uFF1A";
lblMargin.Left = 16; lblMargin.Top = 150; lblMargin.AutoSize = true;
lblMargin.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

var txtMargin = new TextBox();
txtMargin.Left = 96; txtMargin.Top = 146; txtMargin.Width = 64;
txtMargin.Text = MarginPercent.ToString();
txtMargin.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);

var lblMarginUnit = new Label();
lblMarginUnit.Text = "%";
lblMarginUnit.Left = 166; lblMarginUnit.Top = 150; lblMarginUnit.AutoSize = true;
lblMarginUnit.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
```

Move the rotate and center checkboxes down to `Top = 180`, the note to `Top = 214`, and buttons to `Top = 252`.

In `ok.Click`, parse and save:

```csharp
double margin;
MarginPercent = double.TryParse(txtMargin.Text.Trim(), out margin) ? Math.Max(0, margin) : 0;
Config.BatchPlotMarginPercent = MarginPercent;
```

Add the new controls to `Controls.AddRange`.

- [ ] **Step 4: Replace hard-coded plot style items with discovery helper**

Replace:

```csharp
cmbStyle.Items.Add("monochrome.ctb");
cmbStyle.Items.Add("acad.ctb");
```

with:

```csharp
foreach (string style in GetPlotStyleNames())
{
    cmbStyle.Items.Add(style);
}
```

Add helper methods inside `BatchPlotDialog`:

```csharp
static List<string> GetPlotStyleNames()
{
    var names = new List<string>();
    AddPlotStyleName(names, "monochrome.ctb");
    AddPlotStyleName(names, "acad.ctb");
    AddPlotStyleName(names, "grayscale.ctb");

    foreach (string dir in GetPlotStyleSearchDirectories())
    {
        try
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.ctb"))
                AddPlotStyleName(names, Path.GetFileName(file));
            foreach (string file in Directory.GetFiles(dir, "*.stb"))
                AddPlotStyleName(names, Path.GetFileName(file));
        }
        catch { }
    }

    names.Sort(StringComparer.OrdinalIgnoreCase);
    return names;
}

static IEnumerable<string> GetPlotStyleSearchDirectories()
{
    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    foreach (string root in new string[] { appData, localAppData })
    {
        if (string.IsNullOrEmpty(root)) continue;
        yield return Path.Combine(root, "Autodesk");
        yield return Path.Combine(root, "Gstarsoft");
        yield return Path.Combine(root, "ZWSOFT");
    }
}

static void AddPlotStyleName(List<string> names, string name)
{
    if (string.IsNullOrEmpty(name)) return;
    foreach (string existing in names)
    {
        if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return;
    }
    names.Add(name);
}
```

Also add at the top of `Dialogs.cs`:

```csharp
using System.IO;
```

If recursive discovery is needed for real user machines, adjust the helper to scan selected subfolders with `SearchOption.AllDirectories`, but keep exception handling so inaccessible CAD folders cannot break the dialog.

- [ ] **Step 5: Run the test and verify dialog coverage passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: PASS for dialog margin and style assertions.

---

### Task 4: Preserve Device-Specific GstarCAD Flow

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BatchPlotCommands.cs`
- Test: `CadToolkit/tests/BatchPlot.Tests.ps1`

- [ ] **Step 1: Strengthen tests for command answer order**

Keep and, if needed, refine the existing command-builder scoped assertions:

```powershell
$gstarCommandBuilder = [regex]::Match($batchPlotCommands, 'static\s+string\s+BuildGstarPlotCommand[\s\S]*?static\s+BatchPlotSettings\s+ResolveGstarPlotCommandSettings').Value
Assert-Match 'gstarcad plot command confirms continue printing after page setup save answer' $gstarCommandBuilder 'inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("Y"\)\);'
Assert-Match 'gstarcad plot command does not print physical printers to file' $gstarCommandBuilder 'else\s*\{\s*inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("N"\)\);\s*inputs\.Add\(QuoteGstarLispString\("Y"\)\);'
Assert-Match 'batch plot only expands gstar paper names for dwg to pdf device' $batchPlotCommands 'ShouldUseGstarExpandedPaperName'
Assert-Match 'batch plot recognizes dwg to pdf pc3 for expanded paper names' $batchPlotCommands 'DWG To PDF'
```

These assert:

```lisp
; File-output device:
... "W" "<output.pdf>" "N" "Y"

; System printer:
... "W" "N" "N" "Y"
```

- [ ] **Step 2: Run the tests before editing this area**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: PASS for existing GstarCAD command-flow assertions before any polishing edits.

- [ ] **Step 3: Avoid behavior changes unless tests fail**

Do not change:

```csharp
if (!string.IsNullOrEmpty(outputPath))
{
    inputs.Add(QuoteGstarLispString(outputPath));
    inputs.Add(QuoteGstarLispString("N"));
    inputs.Add(QuoteGstarLispString("Y"));
}
else
{
    inputs.Add(QuoteGstarLispString("N"));
    inputs.Add(QuoteGstarLispString("N"));
    inputs.Add(QuoteGstarLispString("Y"));
}
```

The only allowed change in this task is replacing raw frame coordinates with the expanded frame from Task 2.

---

### Task 5: Verification, Build, and Local Deploy

**Files:**
- All changed files from Tasks 1-4

- [ ] **Step 1: Run structural batch plot tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\BatchPlot.Tests.ps1
```

Expected: all assertions print `PASS`.

- [ ] **Step 2: Run config upgrade tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\tests\ConfigUpgrade.Tests.ps1
```

Expected: all assertions print `PASS`.

- [ ] **Step 3: Run whitespace check**

Run:

```powershell
git diff --check
```

Expected: no output and exit code 0.

- [ ] **Step 4: Build and deploy locally**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\CadToolkit\deploy-local.ps1
```

Expected:

- Build succeeds for the enabled CAD targets.
- Existing `C:\CadToolkit\CadToolkit.ini` is preserved.
- Updated DLLs and template files are copied to local deployment.

- [ ] **Step 5: Manual smoke test in GstarCAD**

In GstarCAD after loading CadToolkit:

```text
CC
CT_BATCHPLOT
```

Test with `DWG To PDF.pc3`:

- Select two frame objects.
- Paper `A3`.
- Style `monochrome.ctb`.
- Margin `2`.
- Expected: PDFs are created in the DWG directory, not blank, and the frame has a small paper margin.

Test with `PDF24` or another system printer:

- Select one or two frame objects.
- Paper `A3`.
- Style `monochrome.ctb`.
- Margin `2`.
- Expected: GstarCAD does not ask for `.plt` filename and sends the plot to the printer.

---

## Self-Review

- Spec coverage: device-specific GstarCAD flow is protected in Task 4; style discovery is implemented in Task 3; margin config and plotting behavior are implemented in Tasks 1-2; diagnostics and config preservation are covered in Tasks 1 and 5.
- Placeholder scan: no `TBD`, `TODO`, `implement later`, or vague "handle edge cases" steps remain.
- Type consistency: `BatchPlotMarginPercent` is the config key and property; `MarginPercent` is the dialog/settings field; `ExpandBatchPlotFrame` returns `BatchPlotFrame` and is used by both command and API paths.
