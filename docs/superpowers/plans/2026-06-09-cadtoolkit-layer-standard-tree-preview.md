# CadToolkit Layer Standard Tree Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `CT_LAYERSTANDARD` plain text preview with a grouped `TreeView` preview that is easier to inspect on large drawings.

**Architecture:** Keep the existing matching, migration, fallback, whitelist, and cleanup behavior unchanged. Add testable preview-tree builders in `CadToolkit/src/CadToolkit/Plugin.cs`, then update `CadToolkit/src/CadToolkit/LayerCommands.cs` to render those nodes in a WinForms `TreeView`.

**Tech Stack:** C# .NET Framework 4.x, WinForms `TreeView`/`TreeNode`, existing PowerShell reflection tests, existing AutoCAD stub build.

---

## File Structure

- Modify `CadToolkit/tests/LayerStandardMatching.Tests.ps1`
  - Add reflection tests for tree preview structure, fallback wording, grouping, sorting, and UI source usage.
- Modify `CadToolkit/src/CadToolkit/Plugin.cs`
  - Add `BuildLayerPlanTreeNodes(...)`.
  - Add small helper methods for total counts, sorted plans, and migration grouping.
  - Keep `FormatLayerPlan(...)` for compatibility with existing tests and any future copy-report work.
- Modify `CadToolkit/src/CadToolkit/LayerCommands.cs`
  - Replace the preview `TextBox` with a `TreeView`.
  - Rebuild the `TreeView` when the fallback checkbox changes.
  - Keep bottom checkboxes and execution logic unchanged.

## Task 1: Add Failing Tree Preview Tests

**Files:**
- Modify: `CadToolkit/tests/LayerStandardMatching.Tests.ps1`

- [ ] **Step 1: Add reflection setup for `BuildLayerPlanTreeNodes`**

Append this immediately after the existing `FormatLayerPlan` assertions:

```powershell
$buildTree = $commandsType.GetMethod('BuildLayerPlanTreeNodes', [Reflection.BindingFlags]'NonPublic, Static')
Assert-NotNull 'tree preview helper exists' $buildTree
```

- [ ] **Step 2: Add grouped preview data**

Use the existing `New-Plan`, `$planListType`, and `$rules`. Add one more migration target and extra rows so sorting is observable:

```powershell
[void]$plansForPreview.Add((New-Plan 'TEXT-OLD-A' '3-TEXT' 7 'text reason A'))
[void]$plansForPreview.Add((New-Plan 'TEXT-OLD-B' '3-TEXT' 11 'text reason B'))
[void]$plansForPreview.Add((New-Plan 'EQUIP-LOW' '0-EQUIPMENT' 1 'equipment low reason'))
[void]$fallbackForPreview.Add((New-Plan 'UNKNOWN-BIG' '0' 9 'fallback big reason'))
[void]$whitelistForPreview.Add((New-Plan 'FRAME-BIG' '' 13 'white big reason'))
```

- [ ] **Step 3: Assert top-level nodes and fallback wording**

```powershell
function Node-Text($node) { return [string]$node.Text }

$treeWithoutFallback = $buildTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $false))
Assert-Equal 'tree preview top node count' 4 $treeWithoutFallback.Length
Assert-Contains 'tree summary node text' (Node-Text $treeWithoutFallback[0]) '^摘要'
Assert-Contains 'tree unknown node preserves layers' (Node-Text $treeWithoutFallback[1]) '保持原样'
Assert-Contains 'tree migration node exists' (Node-Text $treeWithoutFallback[2]) '^将迁移图层'
Assert-Contains 'tree whitelist node exists' (Node-Text $treeWithoutFallback[3]) '^白名单图层'

$treeWithFallback = $buildTree.Invoke($null, @($plansForPreview, $fallbackForPreview, $whitelistForPreview, $rules, $true))
Assert-Contains 'tree unknown node moves to 0' (Node-Text $treeWithFallback[1]) '将归到 0 层'
Assert-Contains 'tree unknown child moves to 0' (Node-Text $treeWithFallback[1].Nodes[0]) '-> 0'
```

- [ ] **Step 4: Assert grouping and sorting**

```powershell
$migrationNode = $treeWithoutFallback[2]
Assert-Contains 'tree first migration group sorted by object count' (Node-Text $migrationNode.Nodes[0]) '^3-TEXT'
Assert-Contains 'tree second migration group sorted by object count' (Node-Text $migrationNode.Nodes[1]) '^0-EQUIPMENT'
Assert-Contains 'tree first source sorted by object count' (Node-Text $migrationNode.Nodes[0].Nodes[0]) '^TEXT-OLD-B'
Assert-Contains 'tree second source sorted by object count' (Node-Text $migrationNode.Nodes[0].Nodes[1]) '^TEXT-OLD-A'
Assert-Contains 'tree whitelist child includes reason' (Node-Text $treeWithoutFallback[3].Nodes[0]) 'white big reason'
```

- [ ] **Step 5: Assert `LayerCommands.cs` uses `TreeView`**

Extend the existing source assertions:

```powershell
Assert-Contains 'layer standard preview uses tree view' $layerCommands 'new\s+TreeView\s*\('
Assert-Contains 'layer standard fallback rebuilds tree preview' $layerCommands 'BuildLayerPlanTreePreview'
Assert-NotContains 'layer standard no longer creates text preview variable' $layerCommands 'var\s+txt\s*=\s*new\s+TextBox\s*\('
```

- [ ] **Step 6: Run the focused test and confirm RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: FAIL because `BuildLayerPlanTreeNodes` does not exist or because `LayerCommands.cs` does not yet use `TreeView`.

## Task 2: Implement Tree Preview Builders

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`

- [ ] **Step 1: Add `BuildLayerPlanTreeNodes` near `FormatLayerPlan`**

Add this method before `FormatLayerPlan(...)`:

```csharp
static TreeNode[] BuildLayerPlanTreeNodes(List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0)
{
    var nodes = new List<TreeNode>();
    int migrateObjects = SumLayerPlanCounts(plans);
    int fallbackObjects = SumLayerPlanCounts(fallbackPlans);
    int whitelistObjects = SumLayerPlanCounts(whitelistPlans);

    var summary = new TreeNode(string.Format("摘要：标准图层 {0} 个；将迁移 {1} 层 / {2} 对象；未识别 {3} 层 / {4} 对象；白名单 {5} 层 / {6} 对象",
        rules.Count, plans.Count, migrateObjects, fallbackPlans.Count, fallbackObjects, whitelistPlans.Count, whitelistObjects));
    summary.Nodes.Add(new TreeNode(string.Format("标准图层：{0} 个", rules.Count)));
    summary.Nodes.Add(new TreeNode(string.Format("将迁移：{0} 个旧图层 / {1} 个对象", plans.Count, migrateObjects)));
    summary.Nodes.Add(new TreeNode(string.Format("未识别：{0} 个图层 / {1} 个对象", fallbackPlans.Count, fallbackObjects)));
    summary.Nodes.Add(new TreeNode(string.Format("白名单：{0} 个图层 / {1} 个对象", whitelistPlans.Count, whitelistObjects)));
    summary.Expand();
    nodes.Add(summary);

    var unknown = new TreeNode(string.Format("未识别图层（{0} 层 / {1} 对象，{2}）",
        fallbackPlans.Count, fallbackObjects, fallbackTo0 ? "将归到 0 层" : "保持原样"));
    foreach (var p in SortLayerPlansByCount(fallbackPlans))
    {
        string text = fallbackTo0
            ? string.Format("{0} -> 0    {1} 对象    {2}", p.SourceLayer, p.Count, SafeStr(p.Reason))
            : string.Format("{0}    {1} 对象    保持原样    {2}", p.SourceLayer, p.Count, SafeStr(p.Reason));
        unknown.Nodes.Add(new TreeNode(text));
    }
    unknown.Expand();
    nodes.Add(unknown);

    var migrate = new TreeNode(string.Format("将迁移图层（{0} 层 / {1} 对象）", plans.Count, migrateObjects));
    foreach (var group in BuildLayerPlanTargetGroups(plans))
    {
        var groupNode = new TreeNode(string.Format("{0}（{1} 层 / {2} 对象）", group.TargetLayer, group.Plans.Count, group.Count));
        foreach (var p in SortLayerPlansByCount(group.Plans))
            groupNode.Nodes.Add(new TreeNode(string.Format("{0} -> {1}    {2} 对象    {3}", p.SourceLayer, p.TargetLayer, p.Count, SafeStr(p.Reason))));
        migrate.Nodes.Add(groupNode);
    }
    nodes.Add(migrate);

    var whitelist = new TreeNode(string.Format("白名单图层（{0} 层 / {1} 对象，保持原样）", whitelistPlans.Count, whitelistObjects));
    foreach (var p in SortLayerPlansByCount(whitelistPlans))
        whitelist.Nodes.Add(new TreeNode(string.Format("{0}    {1} 对象    {2}", p.SourceLayer, p.Count, SafeStr(p.Reason))));
    nodes.Add(whitelist);

    return nodes.ToArray();
}
```

- [ ] **Step 2: Add helper class and count/sort helpers**

Add these near the new method:

```csharp
class LayerPlanTargetGroup
{
    public string TargetLayer;
    public int Count;
    public List<LayerStandardPlan> Plans = new List<LayerStandardPlan>();
}

static int SumLayerPlanCounts(List<LayerStandardPlan> plans)
{
    int total = 0;
    foreach (var p in plans) total += p.Count;
    return total;
}

static List<LayerStandardPlan> SortLayerPlansByCount(List<LayerStandardPlan> plans)
{
    var sorted = new List<LayerStandardPlan>(plans);
    sorted.Sort(delegate(LayerStandardPlan a, LayerStandardPlan b)
    {
        int byCount = b.Count.CompareTo(a.Count);
        if (byCount != 0) return byCount;
        return SafeStr(a.SourceLayer).CompareTo(SafeStr(b.SourceLayer));
    });
    return sorted;
}

static List<LayerPlanTargetGroup> BuildLayerPlanTargetGroups(List<LayerStandardPlan> plans)
{
    var byTarget = new Dictionary<string, LayerPlanTargetGroup>(StringComparer.OrdinalIgnoreCase);
    foreach (var p in plans)
    {
        LayerPlanTargetGroup group;
        if (!byTarget.TryGetValue(p.TargetLayer, out group))
        {
            group = new LayerPlanTargetGroup { TargetLayer = p.TargetLayer };
            byTarget[p.TargetLayer] = group;
        }
        group.Count += p.Count;
        group.Plans.Add(p);
    }

    var groups = new List<LayerPlanTargetGroup>(byTarget.Values);
    groups.Sort(delegate(LayerPlanTargetGroup a, LayerPlanTargetGroup b)
    {
        int byCount = b.Count.CompareTo(a.Count);
        if (byCount != 0) return byCount;
        return SafeStr(a.TargetLayer).CompareTo(SafeStr(b.TargetLayer));
    });
    return groups;
}
```

- [ ] **Step 3: Run the focused test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: still FAIL until `LayerCommands.cs` uses `TreeView`; tree helper assertions should pass.

## Task 3: Replace Preview TextBox With TreeView

**Files:**
- Modify: `CadToolkit/src/CadToolkit/LayerCommands.cs`

- [ ] **Step 1: Add a small UI rebuild helper**

Add this method near other layer-standard helper methods:

```csharp
static void BuildLayerPlanTreePreview(TreeView tree, List<LayerStandardPlan> plans, List<LayerStandardPlan> fallbackPlans, List<LayerStandardPlan> whitelistPlans, List<LayerStandardRule> rules, bool fallbackTo0)
{
    tree.BeginUpdate();
    try
    {
        tree.Nodes.Clear();
        tree.Nodes.AddRange(BuildLayerPlanTreeNodes(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0));
    }
    finally
    {
        tree.EndUpdate();
    }
}
```

- [ ] **Step 2: Replace the preview control**

Replace:

```csharp
var txt = new TextBox();
txt.Multiline = true; txt.ReadOnly = true; txt.ScrollBars = ScrollBars.Both;
txt.WordWrap = false; txt.Font = new System.Drawing.Font("Consolas", 9f);
txt.Left = UiScale(12); txt.Top = UiScale(12); txt.Width = UiScale(536); txt.Height = UiScale(300);
txt.Text = FormatLayerPlan(plans, fallbackPlans, whitelistPlans, rules, fallbackTo0);
```

With:

```csharp
var tree = new TreeView();
tree.HideSelection = false;
tree.FullRowSelect = true;
tree.ShowNodeToolTips = true;
tree.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
tree.Left = UiScale(12); tree.Top = UiScale(12); tree.Width = UiScale(536); tree.Height = UiScale(340);
BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, fallbackTo0);
```

- [ ] **Step 3: Move bottom controls down**

Use these positions:

```csharp
chkByLayer.Left = UiScale(12); chkByLayer.Top = UiScale(358); chkByLayer.Width = UiScale(536); chkByLayer.Height = UiScale(24);
chkDelete.Left = UiScale(12); chkDelete.Top = UiScale(386); chkDelete.Width = UiScale(190); chkDelete.Height = UiScale(24);
chkFallback.Left = UiScale(12); chkFallback.Top = UiScale(414); chkFallback.Width = UiScale(536); chkFallback.Height = UiScale(24);
ok.Left = UiScale(376); ok.Top = UiScale(470); ok.Width = UiScale(80); ok.Height = UiScale(28);
cancel.Left = UiScale(468); cancel.Top = UiScale(470); cancel.Width = UiScale(80); cancel.Height = UiScale(28);
```

Set the form size to:

```csharp
f.ClientSize = new Size(UiScale(560), UiScale(510));
```

- [ ] **Step 4: Rebuild tree on fallback change**

Replace:

```csharp
chkFallback.CheckedChanged += delegate { txt.Text = FormatLayerPlan(plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked); };
```

With:

```csharp
chkFallback.CheckedChanged += delegate { BuildLayerPlanTreePreview(tree, plans, fallbackPlans, whitelistPlans, rules, chkFallback.Checked); };
```

- [ ] **Step 5: Add the tree to the form**

Replace:

```csharp
f.Controls.AddRange(new Control[] { txt, chkByLayer, chkDelete, chkFallback, ok, cancel });
```

With:

```csharp
f.Controls.AddRange(new Control[] { tree, chkByLayer, chkDelete, chkFallback, ok, cancel });
```

- [ ] **Step 6: Run the focused test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\LayerStandardMatching.Tests.ps1
```

Expected: PASS.

## Task 4: Verify, Commit, and Prepare Local Deployment

**Files:**
- All changed files above.

- [ ] **Step 1: Run all CadToolkit PowerShell tests**

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

- [ ] **Step 3: Commit the implementation**

Run:

```powershell
git add CadToolkit\tests\LayerStandardMatching.Tests.ps1 CadToolkit\src\CadToolkit\Plugin.cs CadToolkit\src\CadToolkit\LayerCommands.cs
git commit -m "feat(CadToolkit): add layer standard tree preview"
```

Expected: commit succeeds.

- [ ] **Step 4: Before deployment, protect local config**

If deployment is requested, compare the local config and template before running deploy:

```powershell
Get-FileHash -Algorithm SHA256 C:\CadToolkit\CadToolkit.ini
Get-FileHash -Algorithm SHA256 CadToolkit\CadToolkit.ini
```

Expected: hashes may differ; do not overwrite `C:\CadToolkit\CadToolkit.ini` unless the user explicitly asks.

- [ ] **Step 5: Deploy locally only after verification**

Run:

```powershell
.\CadToolkit\deploy-local.ps1
```

Expected: local deployment succeeds. If CAD locks a DLL, report the lock instead of force-closing CAD.

## Self-Review

- Spec coverage: the plan covers the top-level TreeView nodes, default expanded risk nodes, collapsed migration/whitelist nodes by omission of `Expand()`, fallback rebuild behavior, grouping, sorting, and unchanged execution behavior.
- Placeholder scan: no `TBD`, `TODO`, or unexpanded "write tests" steps remain.
- Type consistency: helper names are `BuildLayerPlanTreeNodes`, `BuildLayerPlanTreePreview`, `LayerPlanTargetGroup`, `SumLayerPlanCounts`, `SortLayerPlansByCount`, and `BuildLayerPlanTargetGroups` throughout the plan.
