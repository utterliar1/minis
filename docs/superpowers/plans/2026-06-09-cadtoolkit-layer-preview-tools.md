# CadToolkit Layer Preview Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add display filters and copy-report support to the `CT_LAYERSTANDARD` preview, and sync the default `0-设备层` layer map to the user's local wildcard style.

**Architecture:** Keep the existing layer matching and execution plan untouched. Add a small preview filter enum/helper around the existing `BuildLayerPlanTreeNodes(...)`, wire the WinForms dialog to rebuild the tree for the selected display mode, reuse `FormatLayerPlan(...)` for clipboard output, and update configuration text sources plus tests.

**Tech Stack:** C# .NET Framework 4.x, WinForms `TreeView`/`RadioButton`/`Clipboard`, existing PowerShell reflection tests, existing AutoCAD stub build.

---

## File Structure

- Modify `CadToolkit/tests/LayerStandardMatching.Tests.ps1`
  - Add reflection tests for filtered tree nodes and source checks for filter/copy controls.
  - Update default config assertions from `VIS35` to `*VIS*`.
- Modify `CadToolkit/src/CadToolkit/Plugin.cs`
  - Add `LayerPlanTreeFilter`.
  - Add `BuildFilteredLayerPlanTreeNodes(...)` that returns summary plus the requested section.
- Modify `CadToolkit/src/CadToolkit/LayerCommands.cs`
  - Add filter radio buttons above the tree.
  - Rebuild the tree while preserving the selected filter when fallback changes.
  - Add `复制报告` button using `FormatLayerPlan(...)` and `Clipboard.SetText(...)`.
- Modify `CadToolkit/CadToolkit.ini`
  - Update `0-设备层=*设备*,0-4,*VIS*`.
- Modify `CadToolkit/CadToolkit.default.ini`
  - Update `0-设备层=*设备*,0-4,*VIS*`.
- Modify `CadToolkit/src/CadToolkit.Core/Config.cs`
  - Update embedded default layer map to `*VIS*`.
- Modify `CadToolkit/README.md`
  - Update layer map example/documentation to `*VIS*`.
- Modify `CadToolkit/CadToolkit使用手册.html`
  - Update layer map example/documentation to `*VIS*`.

## Task 1: Add Failing Tests for Preview Filters and Copy Button

**Files:**
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`

- [ ] **Step 1: Add filtered tree helper reflection test**

Add after the existing `tree preview helper exists` assertion:

```powershell
$buildFilteredTree = $commandsType.GetMethod('BuildFilteredLayerPlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'filtered tree preview helper exists' $buildFilteredTree
$filterType = $commandsType.GetNestedType('LayerPlanTreeFilter', [Reflection.BindingFlags]'NonPublic')
Assert-NotNull 'tree preview filter enum exists' $filterType
```

- [ ] **Step 2: Add filter enum values**

Add after the existing `$treeWithoutFallback` and `$treeWithFallback` assertions:

```powershell
$filterAll = [Enum]::Parse($filterType, 'All')
$filterUnknown = [Enum]::Parse($filterType, 'Unknown')
$filterMigration = [Enum]::Parse($filterType, 'Migration')
$filterWhitelistOnly = [Enum]::Parse($filterType, 'Whitelist')
```

- [ ] **Step 3: Assert filtered node shapes**

Add after existing migration/whitelist tree assertions:

```powershell
$filteredAll = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterAll))
Assert-Equal 'filtered tree all node count' 4 $filteredAll.Length
Assert-Contains 'filtered tree all includes unknown' (Node-Text $filteredAll[1]) '^\u672A\u8BC6\u522B\u56FE\u5C42'

$filteredUnknown = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterUnknown))
Assert-Equal 'filtered tree unknown node count' 2 $filteredUnknown.Length
Assert-Contains 'filtered tree unknown keeps summary first' (Node-Text $filteredUnknown[0]) '^\u6458\u8981'
Assert-Contains 'filtered tree unknown shows only unknown section' (Node-Text $filteredUnknown[1]) '^\u672A\u8BC6\u522B\u56FE\u5C42'

$filteredMigration = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterMigration))
Assert-Equal 'filtered tree migration node count' 2 $filteredMigration.Length
Assert-Contains 'filtered tree migration shows only migration section' (Node-Text $filteredMigration[1]) '^\u5C06\u8FC1\u79FB\u56FE\u5C42'

$filteredWhitelistOnly = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false, $filterWhitelistOnly))
Assert-Equal 'filtered tree whitelist node count' 2 $filteredWhitelistOnly.Length
Assert-Contains 'filtered tree whitelist shows only whitelist section' (Node-Text $filteredWhitelistOnly[1]) '^\u767D\u540D\u5355\u56FE\u5C42'

$filteredUnknownFallback = $buildFilteredTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $true, $filterUnknown))
Assert-Contains 'filtered unknown respects fallback to 0' (Node-Text $filteredUnknownFallback[1]) '\u5C06\u5F52\u5230 0 \u5C42'
```

- [ ] **Step 4: Assert UI source contains filter and copy controls**

Add after existing `layer standard fallback rebuilds tree preview` assertion:

```powershell
Assert-Contains 'layer standard preview has all filter button' $layerCommands '\u5168\u90E8'
Assert-Contains 'layer standard preview has unknown filter button' $layerCommands '\u672A\u8BC6\u522B'
Assert-Contains 'layer standard preview has migration filter button' $layerCommands '\u5C06\u8FC1\u79FB'
Assert-Contains 'layer standard preview has whitelist filter button' $layerCommands '\u767D\u540D\u5355'
Assert-Contains 'layer standard preview has copy report button' $layerCommands '\u590D\u5236\u62A5\u544A'
Assert-Contains 'layer standard copy report uses clipboard' $layerCommands 'Clipboard\.SetText'
```

- [ ] **Step 5: Run focused test to confirm RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: FAIL because `BuildFilteredLayerPlanTreeNodes` and `LayerPlanTreeFilter` do not exist.

## Task 2: Implement Filtered Tree Helpers

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`

- [ ] **Step 1: Add filter enum**

Add near `LayerStandardPlan`:

```csharp
        enum LayerPlanTreeFilter
        {
            All,
            Unknown,
            Migration,
            Whitelist
        }
```

- [ ] **Step 2: Add filtered node helper**

Add immediately after `BuildLayerPlanTreeNodes(...)`:

```csharp
        static TreeNode[] BuildFilteredLayerPlanTreeNodes(List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0, LayerPlanTreeFilter filter)
        {
            var allNodes = BuildLayerPlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0);
            if (filter == LayerPlanTreeFilter.All) return allNodes;

            var nodes = new List<TreeNode>();
            nodes.Add((TreeNode)allNodes[0].Clone());
            if (filter == LayerPlanTreeFilter.Unknown) nodes.Add((TreeNode)allNodes[1].Clone());
            if (filter == LayerPlanTreeFilter.Migration) nodes.Add((TreeNode)allNodes[2].Clone());
            if (filter == LayerPlanTreeFilter.Whitelist) nodes.Add((TreeNode)allNodes[3].Clone());
            return nodes.ToArray();
        }
```

- [ ] **Step 3: Run focused test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: helper assertions pass; UI source assertions still fail.

## Task 3: Wire Preview Filter UI

**Files:**
- Modify: `CadToolkit/src/CadToolkit/LayerCommands.cs`

- [ ] **Step 1: Change tree rebuild helper signature**

Replace the current helper with:

```csharp
        static void BuildLayerPlanTreePreview(TreeView tree, List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0, LayerPlanTreeFilter filter)
        {
            tree.BeginUpdate();
            try
            {
                tree.Nodes.Clear();
                tree.Nodes.AddRange(BuildFilteredLayerPlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0, filter));
            }
            finally
            {
                tree.EndUpdate();
            }
        }
```

- [ ] **Step 2: Add selected filter variable and form sizing**

Replace:

```csharp
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(560), UiScale(510));
```

With:

```csharp
            LayerPlanTreeFilter previewFilter = LayerPlanTreeFilter.All;
            f.AutoScaleMode = AutoScaleMode.None; f.AutoScroll = true; f.ClientSize = new Size(UiScale(620), UiScale(540));
```

- [ ] **Step 3: Add filter radio buttons before the tree**

Insert before creating the `TreeView`:

```csharp
            var rbAll = new RadioButton();
            rbAll.Text = "\u5168\u90e8"; rbAll.Left = UiScale(12); rbAll.Top = UiScale(12); rbAll.Width = UiScale(70); rbAll.Height = UiScale(24); rbAll.Checked = true;
            rbAll.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var rbUnknown = new RadioButton();
            rbUnknown.Text = "\u672a\u8bc6\u522b"; rbUnknown.Left = UiScale(88); rbUnknown.Top = UiScale(12); rbUnknown.Width = UiScale(86); rbUnknown.Height = UiScale(24);
            rbUnknown.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var rbMigration = new RadioButton();
            rbMigration.Text = "\u5c06\u8fc1\u79fb"; rbMigration.Left = UiScale(180); rbMigration.Top = UiScale(12); rbMigration.Width = UiScale(86); rbMigration.Height = UiScale(24);
            rbMigration.Font = new System.Drawing.Font("Microsoft YaHei", 9f);

            var rbWhitelist = new RadioButton();
            rbWhitelist.Text = "\u767d\u540d\u5355"; rbWhitelist.Left = UiScale(272); rbWhitelist.Top = UiScale(12); rbWhitelist.Width = UiScale(86); rbWhitelist.Height = UiScale(24);
            rbWhitelist.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
```

- [ ] **Step 4: Move the tree below filter row**

Use:

```csharp
            tree.Left = UiScale(12); tree.Top = UiScale(42); tree.Width = UiScale(596); tree.Height = UiScale(340);
            BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, fallbackTo0, previewFilter);
```

- [ ] **Step 5: Add filter change handler**

Add after tree creation:

```csharp
            EventHandler filterChanged = delegate
            {
                if (rbUnknown.Checked) previewFilter = LayerPlanTreeFilter.Unknown;
                else if (rbMigration.Checked) previewFilter = LayerPlanTreeFilter.Migration;
                else if (rbWhitelist.Checked) previewFilter = LayerPlanTreeFilter.Whitelist;
                else previewFilter = LayerPlanTreeFilter.All;
                BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked, previewFilter);
            };
            rbAll.CheckedChanged += filterChanged;
            rbUnknown.CheckedChanged += filterChanged;
            rbMigration.CheckedChanged += filterChanged;
            rbWhitelist.CheckedChanged += filterChanged;
```

If this fails because `chkFallback` is declared later, move the handler hookup to after `chkFallback` is created while leaving the buttons above the tree.

- [ ] **Step 6: Adjust bottom control positions**

Use:

```csharp
            chkByLayer.Left = UiScale(12); chkByLayer.Top = UiScale(390); chkByLayer.Width = UiScale(596); chkByLayer.Height = UiScale(24);
            chkDelete.Left = UiScale(12); chkDelete.Top = UiScale(418); chkDelete.Width = UiScale(190); chkDelete.Height = UiScale(24);
            chkFallback.Left = UiScale(12); chkFallback.Top = UiScale(446); chkFallback.Width = UiScale(596); chkFallback.Height = UiScale(24);
```

- [ ] **Step 7: Update fallback rebuild handler**

Replace:

```csharp
            chkFallback.CheckedChanged += delegate { BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked); };
```

With:

```csharp
            chkFallback.CheckedChanged += delegate { BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked, previewFilter); };
```

- [ ] **Step 8: Add filter controls to the form**

Replace:

```csharp
            f.Controls.AddRange(new Control[] { tree, chkByLayer, chkDelete, chkFallback, ok, cancel });
```

With:

```csharp
            f.Controls.AddRange(new Control[] { rbAll, rbUnknown, rbMigration, rbWhitelist, tree, chkByLayer, chkDelete, chkFallback, ok, cancel });
```

- [ ] **Step 9: Run focused test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: filter helper and UI filter assertions pass; copy button assertions still fail.

## Task 4: Add Copy Report Button

**Files:**
- Modify: `CadToolkit/src/CadToolkit/LayerCommands.cs`

- [ ] **Step 1: Add copy button**

Add before the `ok` button:

```csharp
            var copy = new Button();
            copy.Text = "\u590d\u5236\u62a5\u544a";
            copy.Left = UiScale(336); copy.Top = UiScale(500); copy.Width = UiScale(88); copy.Height = UiScale(28); copy.FlatStyle = FlatStyle.System;
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(FormatLayerPlan(plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked));
                    MessageBox.Show("\u5df2\u590d\u5236\u62a5\u544a\u3002", "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("\u590d\u5236\u62a5\u544a\u5931\u8d25\uff1a" + ex.Message, "\u63d0\u793a", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
```

- [ ] **Step 2: Shift OK/Cancel buttons**

Use:

```csharp
            ok.Left = UiScale(432); ok.Top = UiScale(500); ok.Width = UiScale(80); ok.Height = UiScale(28);
            cancel.Left = UiScale(528); cancel.Top = UiScale(500); cancel.Width = UiScale(80); cancel.Height = UiScale(28);
```

- [ ] **Step 3: Add copy button to controls**

Use:

```csharp
            f.Controls.AddRange(new Control[] { rbAll, rbUnknown, rbMigration, rbWhitelist, tree, chkByLayer, chkDelete, chkFallback, copy, ok, cancel });
```

- [ ] **Step 4: Run focused test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: PASS except default config assertions still expect old `VIS35`.

## Task 5: Sync Default Layer Map to `*VIS*`

**Files:**
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/CadToolkit.default.ini`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/README.md`
- Modify: `CadToolkit/CadToolkit使用手册.html`
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`

- [ ] **Step 1: Update config files**

Replace exact lines:

```ini
0-设备层=*设备*,0-4,VIS35
```

With:

```ini
0-设备层=*设备*,0-4,*VIS*
```

In:

```powershell
CadToolkit\CadToolkit.ini
CadToolkit\CadToolkit.default.ini
```

- [ ] **Step 2: Update embedded default config**

In `CadToolkit/src/CadToolkit.Core/Config.cs`, replace the embedded equipment layer map line so the emitted text contains:

```csharp
sb.AppendLine("0-\u8BBE\u5907\u5C42=*\u8BBE\u5907*,0-4,*VIS*");
```

- [ ] **Step 3: Update README and manual**

Replace examples/documentation occurrences of:

```text
0-设备层=*设备*,0-4,VIS35
```

With:

```text
0-设备层=*设备*,0-4,*VIS*
```

In:

```powershell
CadToolkit\README.md
CadToolkit\CadToolkit使用手册.html
```

- [ ] **Step 4: Update test expected patterns**

In `CadToolkit/tests/LayerStandardMatching.Tests.ps1`, replace:

```powershell
$equipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,VIS35'
$localEquipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,(VIS35|\*VIS\*)'
$embeddedEquipmentLinePattern = '0-\\u8BBE\\u5907\\u5C42=\*\\u8BBE\\u5907\*,0-4,VIS35'
```

With:

```powershell
$equipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,\*VIS\*'
$localEquipmentLinePattern = '0-\u8BBE\u5907\u5C42=\*\u8BBE\u5907\*,0-4,\*VIS\*'
$embeddedEquipmentLinePattern = '0-\\u8BBE\\u5907\\u5C42=\*\\u8BBE\\u5907\*,0-4,\*VIS\*'
```

- [ ] **Step 5: Run focused test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: PASS.

- [ ] **Step 6: Commit implementation**

Run:

```powershell
git add CadToolkit\tests\LayerStandardMatching.Tests.ps1 CadToolkit\src\CadToolkit\Plugin.cs CadToolkit\src\CadToolkit\LayerCommands.cs CadToolkit\CadToolkit.ini CadToolkit\CadToolkit.default.ini CadToolkit\src\CadToolkit.Core\Config.cs CadToolkit\README.md CadToolkit\CadToolkit使用手册.html
git commit -m "feat(CadToolkit): add layer preview filters and report copy"
```

Expected: commit succeeds.

## Task 6: Verify and Deploy

**Files:**
- All changed files above.

- [ ] **Step 1: Run all CadToolkit tests**

Run:

```powershell
Get-ChildItem CadToolkit\tests -Filter *.ps1 | ForEach-Object { Write-Host "RUN $($_.Name)"; & powershell -NoProfile -ExecutionPolicy Bypass -File $_.FullName; if ($LASTEXITCODE) { exit $LASTEXITCODE } }
```

Expected: every script exits with code `0`.

- [ ] **Step 2: Check diff hygiene**

Run:

```powershell
git diff --check
```

Expected: no output and exit code `0`.

- [ ] **Step 3: Deploy locally only after verification**

Before deployment, check for CAD locks:

```powershell
Get-Process | Where-Object { $_.ProcessName -match 'acad|zwcad|gcad|gstar|cad' } | Select-Object ProcessName,Id,MainWindowTitle
```

If no CAD process is using the DLLs, run:

```powershell
.\CadToolkit\deploy-local.ps1
```

Expected: AutoCAD, ZWCAD, and GstarCAD deploy successfully; output reports `Config: existing CadToolkit.ini preserved`.

## Self-Review

- Spec coverage: the plan covers filter UI, filter helper behavior, copy report, default map sync, local config preservation, tests, and deployment.
- Placeholder scan: no `TBD`, `TODO`, or vague "write tests" entries remain.
- Type consistency: helper names are `LayerPlanTreeFilter`, `BuildFilteredLayerPlanTreeNodes`, and `BuildLayerPlanTreePreview` throughout.
