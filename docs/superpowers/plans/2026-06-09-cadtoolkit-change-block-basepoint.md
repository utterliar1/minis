# CadToolkit Change Block Basepoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `CT_CHANGEBASEPOINT` command that changes a block definition base point while keeping all existing same-name block references visually stationary.

**Architecture:** Implement the command inside the existing `CadCommands` partial class in `BlockCommands.cs`. Convert the user-picked world point into the selected block reference's definition coordinates, update the block definition `Origin`, then compensate every same-definition `BlockReference.Position` by the corresponding world-space shift so geometry does not appear to move.

**Tech Stack:** C# 7 style AutoCAD/ZWCAD/GstarCAD .NET APIs, existing CadToolkit partial command structure, PowerShell static regression tests, existing local deploy script.

---

### Task 1: Add Failing Static Coverage

**Files:**
- Create: `CadToolkit/tests/ChangeBlockBasepoint.Tests.ps1`

- [ ] **Step 1: Create the failing test file**

Add this complete test file:

```powershell
$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$blockCommands = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit\BlockCommands.cs') -Raw
$projectConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.ini') -Raw
$defaultConfig = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit.default.ini') -Raw
$configSource = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\src\CadToolkit.Core\Config.cs') -Raw
$readme = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\README.md') -Raw
$manual = Get-Content -Encoding UTF8 (Join-Path $repo 'CadToolkit\CadToolkit使用手册.html') -Raw

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name did not find pattern: $pattern"
    }
    Write-Host "PASS $name"
}

function Assert-ContainsLiteral($name, $text, $literal) {
    if (-not $text.Contains($literal)) {
        throw "$name did not find literal: $literal"
    }
    Write-Host "PASS $name"
}

Assert-Contains 'change basepoint command is registered' $blockCommands '\[CommandMethod\("CT_CHANGEBASEPOINT"\)\]'
Assert-Contains 'change basepoint method exists' $blockCommands 'public\s+void\s+ChangeBlockBasepoint\s*\('
Assert-Contains 'change basepoint asks for block reference' $blockCommands 'AddAllowedClass\(typeof\(BlockReference\),\s*true\)'
Assert-Contains 'change basepoint asks for new base point' $blockCommands '指定新的块基点'
Assert-Contains 'change basepoint converts world point to definition coordinates' $blockCommands 'BlockTransform\.Inverse\(\)'
Assert-Contains 'change basepoint updates block definition origin' $blockCommands '\.Origin\s*=\s*newOrigin'
Assert-Contains 'change basepoint compensates references by position' $blockCommands '\.Position\s*=\s*.*\.Position\s*\+\s*shift'
Assert-Contains 'change basepoint counts affected references' $blockCommands 'affectedReferences'
Assert-Contains 'change basepoint rejects unsupported block records' $blockCommands 'CanChangeBlockBasepoint'
Assert-Contains 'change basepoint scans all block references' $blockCommands 'GetBlockReferencesForDefinition'

Assert-ContainsLiteral 'project config contains command label' $projectConfig '改块基点=CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'default config contains command label' $defaultConfig '改块基点=CT_CHANGEBASEPOINT'
Assert-Contains 'embedded default config contains command label' $configSource '改块基点=CT_CHANGEBASEPOINT|\\u6539\\u5757\\u57FA\\u70B9=CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'readme documents command label' $readme '改块基点'
Assert-ContainsLiteral 'readme documents command name' $readme 'CT_CHANGEBASEPOINT'
Assert-ContainsLiteral 'readme shows four block tools' $readme '图块操作（4 个）'
Assert-ContainsLiteral 'manual documents command label' $manual '改块基点'
Assert-ContainsLiteral 'manual documents command name' $manual 'CT_CHANGEBASEPOINT'
```

- [ ] **Step 2: Run the new test and verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ChangeBlockBasepoint.Tests.ps1
```

Expected: the test fails at `change basepoint command is registered` because `CT_CHANGEBASEPOINT` does not exist yet.

- [ ] **Step 3: Commit the failing test**

Run:

```powershell
git add CadToolkit/tests/ChangeBlockBasepoint.Tests.ps1
git commit -m "test(CadToolkit): cover change block basepoint command"
```

---

### Task 2: Implement The CAD Command

**Files:**
- Modify: `CadToolkit/src/CadToolkit/BlockCommands.cs`

- [ ] **Step 1: Add the command method and helpers**

Insert this code inside `public partial class CadCommands`, after `QuickBlock()` and before `SelectByBlock()`:

```csharp
[CommandMethod("CT_CHANGEBASEPOINT")]
        public void ChangeBlockBasepoint()
        {
            EnsureInit();
            if (!CheckDoc()) return;

            var peo = new PromptEntityOptions("\n选择要改基点的块：");
            peo.SetRejectMessage("\n只能选择块参照。");
            peo.AddAllowedClass(typeof(BlockReference), true);
            var per = Ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string blockName = "";
            string rejectReason = "";
            Point3d oldOrigin = Point3d.Origin;
            Point3d newOrigin = Point3d.Origin;
            ObjectId blockDefId = default(ObjectId);
            ObjectId[] referenceIds = null;

            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var br = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                blockName = btr.Name;
                blockDefId = br.BlockTableRecord;

                if (!CanChangeBlockBasepoint(br, btr, out rejectReason))
                {
                    Ed.WriteMessage("\n当前块不支持改基点：" + rejectReason);
                    return;
                }

                oldOrigin = btr.Origin;
                tr.Commit();
            }

            var ppo = new PromptPointOptions("\n指定新的块基点：");
            ppo.AllowNone = false;
            var ppr = Ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return;

            bool changed = RunWithUndo("CT_CHANGEBASEPOINT", delegate
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var selectedBr = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    var selectedBtr = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForWrite);
                    if (!CanChangeBlockBasepoint(selectedBr, selectedBtr, out rejectReason))
                    {
                        Ed.WriteMessage("\n当前块不支持改基点：" + rejectReason);
                        return false;
                    }

                    oldOrigin = selectedBtr.Origin;
                    newOrigin = ppr.Value.TransformBy(selectedBr.BlockTransform.Inverse());
                    referenceIds = GetBlockReferencesForDefinition(tr, blockDefId);

                    var shifts = new Dictionary<ObjectId, Vector3d>();
                    foreach (ObjectId id in referenceIds)
                    {
                        var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;
                        Point3d oldBasePoint = oldOrigin.TransformBy(br.BlockTransform);
                        Point3d newBasePoint = newOrigin.TransformBy(br.BlockTransform);
                        shifts[id] = oldBasePoint.GetVectorTo(newBasePoint);
                    }

                    selectedBtr.Origin = newOrigin;

                    foreach (var pair in shifts)
                    {
                        var br = tr.GetObject(pair.Key, OpenMode.ForWrite) as BlockReference;
                        if (br == null) continue;
                        Vector3d shift = pair.Value;
                        br.Position = br.Position + shift;
                    }

                    tr.Commit();
                }
                return true;
            });

            if (!changed) return;
            int affectedReferences = referenceIds == null ? 0 : referenceIds.Length;
            Ed.WriteMessage(string.Format("\n已修改块 \"{0}\" 的基点，影响 {1} 个同名参照。", blockName, affectedReferences));
        }

        static bool CanChangeBlockBasepoint(BlockReference br, BlockTableRecord btr, out string reason)
        {
            reason = "";
            if (br == null || btr == null) { reason = "块数据无效"; return false; }
            if (TryGetBoolProperty(br, "IsDynamicBlock")) { reason = "动态块暂不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsDynamicBlock")) { reason = "动态块暂不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsAnonymous")) { reason = "匿名块暂不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsLayout")) { reason = "布局块不支持"; return false; }
            if (TryGetBoolProperty(btr, "IsFromExternalReference") || TryGetBoolProperty(btr, "IsFromOverlayReference") || TryGetBoolProperty(btr, "IsDependent"))
            {
                reason = "外部参照或依赖块不支持";
                return false;
            }
            return true;
        }

        static ObjectId[] GetBlockReferencesForDefinition(Transaction tr, ObjectId blockDefId)
        {
            var ids = new List<ObjectId>();
            var bt = (BlockTable)tr.GetObject(Db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                var owner = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (owner == null) continue;
                if (TryGetBoolProperty(owner, "IsFromExternalReference") || TryGetBoolProperty(owner, "IsFromOverlayReference") || TryGetBoolProperty(owner, "IsDependent")) continue;
                foreach (ObjectId entId in owner)
                {
                    var br = tr.GetObject(entId, OpenMode.ForRead) as BlockReference;
                    if (br == null) continue;
                    if (br.BlockTableRecord == blockDefId) ids.Add(entId);
                }
            }
            return ids.ToArray();
        }
```

- [ ] **Step 2: Run the new test**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ChangeBlockBasepoint.Tests.ps1
```

Expected: the code-related assertions pass, and the test then fails on the first missing config or documentation assertion.

- [ ] **Step 3: Build all three CAD targets**

Run:

```powershell
.\CadToolkit\build-all.bat
```

Expected: AutoCAD, ZWCAD, and GstarCAD builds exit successfully. If a compile error appears around CAD API names, adjust only the failing API call to match the SDK/stub while preserving the `Origin + Position` compensation algorithm.

---

### Task 3: Wire Command Into Config And Docs

**Files:**
- Modify: `CadToolkit/CadToolkit.ini`
- Modify: `CadToolkit/CadToolkit.default.ini`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/README.md`
- Modify: `CadToolkit/CadToolkit使用手册.html`

- [ ] **Step 1: Add the command to both INI templates**

In both INI files, change the block command section to:

```ini
# 图块操作
重命名块=CT_RENAMEBLOCK
快捷建块=CT_QUICKBLOCK
改块基点=CT_CHANGEBASEPOINT
按块选择=CT_SELECTBYBLOCK
```

- [ ] **Step 2: Add the command to embedded defaults**

In `CadToolkit/src/CadToolkit.Core/Config.cs`, update the block command section in `GetDefaultConfigText()` to:

```csharp
            sb.AppendLine("# \u56FE\u5757\u64CD\u4F5C");
            sb.AppendLine("\u91CD\u547D\u540D\u5757=CT_RENAMEBLOCK");
            sb.AppendLine("\u5FEB\u6377\u5EFA\u5757=CT_QUICKBLOCK");
            sb.AppendLine("\u6539\u5757\u57FA\u70B9=CT_CHANGEBASEPOINT");
            sb.AppendLine("\u6309\u5757\u9009\u62E9=CT_SELECTBYBLOCK");
```

- [ ] **Step 3: Update README command table**

Change the heading to:

```markdown
### 图块操作（4 个）
```

Make the block command table include:

```markdown
| 重命名块 | `CT_RENAMEBLOCK` | 点选一个块参照，自动识别块名，在弹窗中输入新名称完成重命名。无需手动输入旧块名。 |
| 快捷建块 | `CT_QUICKBLOCK` | 选择对象 → 指定基点 → 自动创建块。块名使用配置的前缀 + 自动递增编号（如 BK001、BK002）。可在配置中设置是否删除原对象。 |
| 改块基点 | `CT_CHANGEBASEPOINT` | 选择一个普通块参照 → 指定新的块基点；插件会更新同名块定义并补偿现有参照位置，使图面上的块图形保持不动。 |
| 按块选择 | `CT_SELECTBYBLOCK` | 先选源块参照获取块名，再选择搜索范围；直接回车则搜索全图。 |
```

- [ ] **Step 4: Update the HTML manual**

In the 图块操作 section, add this block between 快捷建块 and 按块选择:

```html
  <h3>改块基点 <span class="cmd-tag">CT_CHANGEBASEPOINT</span></h3>
  <p>选择一个普通块参照，再指定新的块基点。插件会修改同名块定义，并补偿已有同名块参照的位置，使图面上的块图形保持不动。</p>
```

- [ ] **Step 5: Run the new test and verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CadToolkit\tests\ChangeBlockBasepoint.Tests.ps1
```

Expected: every assertion prints `PASS`.

- [ ] **Step 6: Commit implementation and docs**

Run:

```powershell
git add CadToolkit/src/CadToolkit/BlockCommands.cs CadToolkit/CadToolkit.ini CadToolkit/CadToolkit.default.ini CadToolkit/src/CadToolkit.Core/Config.cs CadToolkit/README.md CadToolkit/CadToolkit使用手册.html CadToolkit/tests/ChangeBlockBasepoint.Tests.ps1
git commit -m "feat(CadToolkit): add change block basepoint command"
```

---

### Task 4: Verify, Deploy Locally, And Hand Off For CAD Testing

**Files:**
- No planned source edits. This task verifies the result and deploys only after the CAD processes are closed.

- [ ] **Step 1: Run all CadToolkit tests**

Run:

```powershell
Get-ChildItem CadToolkit\tests -Filter *.ps1 | ForEach-Object {
    Write-Host "RUN $($_.Name)"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $_.FullName
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}
```

Expected: every test prints its `PASS` lines and the command exits with code 0.

- [ ] **Step 2: Check whitespace**

Run:

```powershell
git diff --check
```

Expected: no output and exit code 0.

- [ ] **Step 3: Build all targets**

Run:

```powershell
.\CadToolkit\build-all.bat
```

Expected: all three target builds complete successfully.

- [ ] **Step 4: Check for running CAD processes**

Run:

```powershell
Get-Process | Where-Object { $_.ProcessName -match 'acad|zwcad|gcad|gstar|cad' } | Select-Object ProcessName,Id,MainWindowTitle
```

Expected: no AutoCAD, ZWCAD, or GstarCAD process is holding `C:\CadToolkit` DLLs. If CAD is still running, stop here and ask the user to close it.

- [ ] **Step 5: Deploy locally**

Run:

```powershell
.\CadToolkit\deploy-local.ps1
```

Expected: AutoCAD, ZWCAD, and GstarCAD DLLs are deployed under `C:\CadToolkit`, and the output says the existing user config is preserved.

- [ ] **Step 6: Ask for manual CAD verification**

Ask the user to test this exact scenario:

```text
1. 打开一张包含普通块的测试图。
2. 运行 CC，确认能看到“改块基点”。
3. 运行 CT_CHANGEBASEPOINT。
4. 选择一个普通块。
5. 在块图形内指定一个新基点。
6. 观察块图形是否保持原位。
7. 再次选中块，确认夹点/插入点已经到新基点附近。
8. 如果图里有多个同名块，确认它们的图形也保持原位。
```

Expected: command可用，普通块图形不漂移，新插入点落到指定基点。
