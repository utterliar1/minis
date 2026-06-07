# CadToolkit 重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 分三阶段提升 CadToolkit 的可维护性、撤销体验、异常可见性和版本一致性。

**Architecture:** 使用 `partial class CadCommands` 保持 CAD 命令注册方式不变，把命令按功能域拆到多个文件。UI 构建逻辑逐步移到 `CadToolkit.UI`，核心配置逻辑仍保留在 `CadToolkit.Core`。

**Tech Stack:** C# / .NET Framework 4.8 / WinForms / AutoCAD、ZWCAD、GstarCAD 托管 API / MSBuild / batch + GitHub Actions。

---

## 文件结构

需要修改或新增的文件：

- 修改：`CadToolkit/src/CadToolkit/Plugin.cs`
- 新增：`CadToolkit/src/CadToolkit/TextCommands.cs`
- 新增：`CadToolkit/src/CadToolkit/LayerCommands.cs`
- 新增：`CadToolkit/src/CadToolkit/BlockCommands.cs`
- 新增：`CadToolkit/src/CadToolkit/DrawCommands.cs`
- 修改：`CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`
- 修改：`CadToolkit/src/CadToolkit/CadToolkit.ZWCAD.csproj`
- 修改：`CadToolkit/src/CadToolkit/CadToolkit.GstarCAD.csproj`
- 修改：`CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- 新增：`CadToolkit/src/CadToolkit.UI/PanelBuilder.cs`
- 修改：`CadToolkit/src/CadToolkit.UI/CadToolkit.UI.csproj`
- 修改：`CadToolkit/src/CadToolkit.Core/Config.cs`
- 修改：`CadToolkit/src/CadToolkit.Core/CadToolkit.Core.csproj`
- 修改：`CadToolkit/autoload.lsp`
- 修改：`CadToolkit/build-all.bat`
- 修改：`.github/workflows/cadtoolkit.yml`

---

## Task 1: 加入基础验证与保护点

**Files:**
- Read: `CadToolkit/src/CadToolkit/Plugin.cs`
- Read: `CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`
- Read: `CadToolkit/src/CadToolkit/CadToolkit.ZWCAD.csproj`
- Read: `CadToolkit/src/CadToolkit/CadToolkit.GstarCAD.csproj`

- [ ] **Step 1: 记录当前状态**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" status --short --branch
git -C "D:\Documents\GitHub\minis" log --oneline --decorate --max-count=5
```

Expected:

```text
## main...origin/main
```

如果出现未提交改动，先判断是否与 CadToolkit 相关。无关改动不要回退。

- [ ] **Step 2: 尝试现有构建入口**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
CadToolkit Build
```

如果本机缺少 CAD DLL 或 MSBuild，只记录失败原因，不修改构建脚本来绕过真实依赖。

- [ ] **Step 3: 提交保护点**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" status --short
```

Expected:

```text
没有输出，或只有本计划文档相关改动
```

---

## Task 2: 阶段 1 - 为批量命令添加 Undo 组

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`

- [ ] **Step 1: 新增 Undo 包装辅助方法**

在 `CadCommands` 类公共辅助方法区域加入：

```csharp
static void RunWithUndo(string name, Action action)
{
    bool started = false;
    try
    {
        Db.StartUndoMark();
        started = true;
        action();
    }
    catch (System.Exception ex)
    {
        Ed.WriteMessage(string.Format("\n{0} 执行失败：{1}", name, ex.Message));
        Log(name + " failed: " + ex);
    }
    finally
    {
        if (started)
        {
            try { Db.EndUndoMark(); }
            catch (System.Exception ex) { Log(name + " EndUndoMark failed: " + ex.Message); }
        }
    }
}
```

- [ ] **Step 2: 包装 `CT_ALIGN`**

把 `AlignText()` 中进入选择后的批量修改逻辑放入：

```csharp
RunWithUndo("CT_ALIGN", delegate
{
    // 原来的对齐事务逻辑保持不变
});
```

Expected behavior:

```text
执行 CT_ALIGN 后，Ctrl+Z 一次回退整个对齐操作。
```

- [ ] **Step 3: 包装 `CT_QUICKBLOCK`**

在用户完成选择和基点输入后，包裹创建块和删除原对象的事务逻辑：

```csharp
RunWithUndo("CT_QUICKBLOCK", delegate
{
    // 原来的建块事务逻辑保持不变
});
```

- [ ] **Step 4: 包装 `CT_LAYERSTANDARD`**

在预览窗口确认后，包裹实际创建图层、迁移对象、删除空层的事务逻辑：

```csharp
RunWithUndo("CT_LAYERSTANDARD", delegate
{
    // 原来的图层规范化事务逻辑保持不变
});
```

- [ ] **Step 5: 包装 `CT_SETLAYER0`**

包裹对象归 0 和块定义归 0 的事务逻辑：

```csharp
RunWithUndo("CT_SETLAYER0", delegate
{
    // 原来的归 0 事务逻辑保持不变
});
```

- [ ] **Step 6: 包装 `CT_TEXTMERGE`**

包裹新建 MText 的事务逻辑：

```csharp
RunWithUndo("CT_TEXTMERGE", delegate
{
    // 原来的合并事务逻辑保持不变
});
```

- [ ] **Step 7: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 8: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit/Plugin.cs"
git -C "D:\Documents\GitHub\minis" commit -m "fix(CadToolkit): add undo groups for batch commands"
```

---

## Task 3: 阶段 1 - 改进静默异常处理

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`

- [ ] **Step 1: 改造 `Plugin.cs` 中的静默 catch**

将关键路径的 `catch { }` 改成：

```csharp
catch (System.Exception ex)
{
    Log("Context message: " + ex);
    Ed.WriteMessage("\n警告：" + ex.Message);
}
```

用于这些位置：

```text
EnsureInit AssemblyResolve
EnsureLineType
ApplyLayerRule 属性设置
LayerStandard 对象迁移失败
SetBlockLayer0 递归处理
FlattenZ fallback TransformBy
```

对于不适合向用户刷屏的位置，只保留：

```csharp
catch (System.Exception ex)
{
    Log("Context message: " + ex.Message);
}
```

- [ ] **Step 2: 改造 `Config.cs` 读写异常**

把 `catch { }` 改为：

```csharp
catch (System.Exception ex)
{
    LogConfigError("配置读取失败: " + ex.Message);
}
```

在 `Config` 类中加入：

```csharp
static void LogConfigError(string msg)
{
    try
    {
        string logPath = Path.Combine(Path.GetTempPath(), "CadToolkit.log");
        File.AppendAllText(logPath, string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg), Encoding.UTF8);
    }
    catch { }
}
```

- [ ] **Step 3: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 4: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit/Plugin.cs" "CadToolkit/src/CadToolkit.Core/Config.cs"
git -C "D:\Documents\GitHub\minis" commit -m "chore(CadToolkit): log recoverable errors"
```

---

## Task 4: 阶段 2 - 拆分命令文件

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`
- Create: `CadToolkit/src/CadToolkit/TextCommands.cs`
- Create: `CadToolkit/src/CadToolkit/LayerCommands.cs`
- Create: `CadToolkit/src/CadToolkit/BlockCommands.cs`
- Create: `CadToolkit/src/CadToolkit/DrawCommands.cs`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.AutoCAD.csproj`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.ZWCAD.csproj`
- Modify: `CadToolkit/src/CadToolkit/CadToolkit.GstarCAD.csproj`

- [ ] **Step 1: 将主类改为 partial**

在 `Plugin.cs` 中：

```csharp
public partial class CadCommands
```

- [ ] **Step 2: 新建命令文件模板**

每个新文件使用相同的平台 using 区块，示例：

```csharp
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Collections;
using CadToolkit.Core;
using CadToolkit.UI;
using LayerStandardRule = CadToolkit.Core.Config.LayerStandardRule;

#if AUTOCAD
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using CadColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;
using CadColor = GrxCAD.Colors.Color;
using CadColorMethod = GrxCAD.Colors.ColorMethod;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using CadColor = ZwSoft.ZwCAD.Colors.Color;
using CadColorMethod = ZwSoft.ZwCAD.Colors.ColorMethod;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
    }
}
```

- [ ] **Step 3: 移动文字命令**

从 `Plugin.cs` 移到 `TextCommands.cs`：

```text
CT_FINDREPLACE
ReplaceInBlock
CT_ALIGN
CT_UNDERLINE
CT_TEXTBRUSH
CT_TEXTMERGE
CT_TEXTNUMBER
```

- [ ] **Step 4: 移动图层命令**

从 `Plugin.cs` 移到 `LayerCommands.cs`：

```text
LayerStandardPlan
MatchLayerRule
SimpleWildcardMatch
IsLayerWhitelisted
FormatLayerPlan
CT_LAYERSTANDARD
CT_SETLAYER0
SetBlockLayer0
CT_ISOLAYER
CT_SELECTBYLAYER
CT_SELECTBYCOLOR
```

如果 `SimpleWildcardMatch` 和 `IsLayerWhitelisted` 后续还被其他文件使用，保留在 `Plugin.cs` 作为 shared helper。

- [ ] **Step 5: 移动图块命令**

从 `Plugin.cs` 移到 `BlockCommands.cs`：

```text
CT_RENAMEBLOCK
CT_QUICKBLOCK
CT_SELECTBYBLOCK
```

- [ ] **Step 6: 移动绘图命令**

从 `Plugin.cs` 移到 `DrawCommands.cs`：

```text
CT_CENTERLINE
CT_QUICKDIM
CT_INCCOPY
CT_FLATTEN
```

- [ ] **Step 7: 更新 3 个平台 csproj**

在每个 `CadToolkit.*.csproj` 中把：

```xml
<ItemGroup><Compile Include="Plugin.cs" /></ItemGroup>
```

替换为：

```xml
<ItemGroup>
  <Compile Include="Plugin.cs" />
  <Compile Include="TextCommands.cs" />
  <Compile Include="LayerCommands.cs" />
  <Compile Include="BlockCommands.cs" />
  <Compile Include="DrawCommands.cs" />
</ItemGroup>
```

- [ ] **Step 8: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 9: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit"
git -C "D:\Documents\GitHub\minis" commit -m "refactor(CadToolkit): split commands by domain"
```

---

## Task 5: 阶段 2 - 提取选择模式与缓存 UiScale

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`
- Modify: `CadToolkit/src/CadToolkit/TextCommands.cs`
- Modify: `CadToolkit/src/CadToolkit/LayerCommands.cs`
- Modify: `CadToolkit/src/CadToolkit/BlockCommands.cs`
- Modify: `CadToolkit/src/CadToolkit/DrawCommands.cs`

- [ ] **Step 1: 新增 `GetSelectionOrAbort`**

在 shared helper 区域加入：

```csharp
static ObjectId[] GetSelectionOrAbort()
{
    EnsureInit();
    if (!CheckDoc()) return null;
    var psr = GetPendingOrSelection();
    if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
    {
        Ed.WriteMessage("\n未选择对象。");
        return null;
    }
    return psr.Value.GetObjectIds();
}
```

- [ ] **Step 2: 替换使用 `GetPendingOrSelection()` 的命令**

把这些命令中的重复选择逻辑替换为：

```csharp
ObjectId[] selectedIds = GetSelectionOrAbort();
if (selectedIds == null) return;
```

适用命令：

```text
CT_ALIGN
CT_UNDERLINE
CT_QUICKBLOCK
CT_SETLAYER0
CT_CENTERLINE
CT_TEXTBRUSH 目标选择
CT_TEXTMERGE
CT_TEXTNUMBER
CT_QUICKDIM
CT_INCCOPY
CT_FLATTEN
```

- [ ] **Step 3: 缓存 UiScale**

替换 `UiScale` 为：

```csharp
static double _uiScaleFactor = 0;

static int UiScale(int value)
{
    try
    {
        if (_uiScaleFactor <= 0)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                _uiScaleFactor = g.DpiX / 96.0;
        }
        return Math.Max(1, (int)Math.Round(value * _uiScaleFactor));
    }
    catch { return value; }
}
```

- [ ] **Step 4: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 5: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit"
git -C "D:\Documents\GitHub\minis" commit -m "refactor(CadToolkit): extract shared selection helper"
```

---

## Task 6: 阶段 2 - 移动 TextNumber 对话框

**Files:**
- Modify: `CadToolkit/src/CadToolkit.UI/Dialogs.cs`
- Modify: `CadToolkit/src/CadToolkit/TextCommands.cs`

- [ ] **Step 1: 在 `Dialogs.cs` 新增 `TextNumberDialog`**

加入：

```csharp
public class TextNumberDialog : Form
{
    public string Prefix;
    public string Suffix;
    public int StartNumber;
    public bool ReplaceOriginal;

    public TextNumberDialog()
    {
        Prefix = ""; Suffix = ""; StartNumber = 1; ReplaceOriginal = false;
        Text = "\u6587\u5b57\u7f16\u53f7";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None; AutoScroll = true; ClientSize = new Size(320, 132);

        var l1 = new Label(); l1.Text = "\u524d\u7f00\uff1a"; l1.Left = 16; l1.Top = 16; l1.AutoSize = true; l1.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
        var t1 = new TextBox(); t1.Left = 76; t1.Top = 12; t1.Width = 70; t1.Text = ""; t1.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
        var l2 = new Label(); l2.Text = "\u540e\u7f00\uff1a"; l2.Left = 160; l2.Top = 16; l2.AutoSize = true; l2.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
        var t2 = new TextBox(); t2.Left = 220; t2.Top = 12; t2.Width = 70; t2.Text = ""; t2.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
        var l3 = new Label(); l3.Text = "\u8d77\u59cb\u53f7\uff1a"; l3.Left = 16; l3.Top = 52; l3.AutoSize = true; l3.Font = new System.Drawing.Font("Microsoft YaHei", 9.5f);
        var t3 = new TextBox(); t3.Left = 76; t3.Top = 48; t3.Width = 70; t3.Text = "1"; t3.Font = new System.Drawing.Font("Microsoft YaHei", 10f);
        var chkReplace = new CheckBox(); chkReplace.Text = "\u66ff\u6362\uff08\u7528\u7f16\u53f7\u66ff\u6362\u539f\u6587\uff09"; chkReplace.Left = 160; chkReplace.Top = 50; chkReplace.Width = 150; chkReplace.Height = 24; chkReplace.Font = new System.Drawing.Font("Microsoft YaHei", 9f);
        var ok = new Button(); ok.Text = "\u786e\u5b9a"; ok.DialogResult = DialogResult.OK; ok.Left = 132; ok.Top = 92; ok.Width = 80; ok.Height = 28; ok.FlatStyle = FlatStyle.System;
        var cancel = new Button(); cancel.Text = "\u53d6\u6d88"; cancel.DialogResult = DialogResult.Cancel; cancel.Left = 224; cancel.Top = 92; cancel.Width = 76; cancel.Height = 28; cancel.FlatStyle = FlatStyle.System;

        ok.Click += delegate
        {
            Prefix = t1.Text.Trim();
            Suffix = t2.Text.Trim();
            int n;
            StartNumber = int.TryParse(t3.Text.Trim(), out n) ? n : 1;
            ReplaceOriginal = chkReplace.Checked;
        };

        Controls.AddRange(new Control[] { l1, t1, l2, t2, l3, t3, chkReplace, ok, cancel });
        AcceptButton = ok; CancelButton = cancel;
        Shown += delegate { t1.Focus(); };
        DpiUtil.Apply(this);
    }
}
```

- [ ] **Step 2: 简化 `CT_TEXTNUMBER`**

把内联 Form 构建替换为：

```csharp
using (var dlg = new TextNumberDialog())
{
    if (dlg.ShowDialog() != DialogResult.OK) return;
    prefix = dlg.Prefix;
    suffix = dlg.Suffix;
    startNum = dlg.StartNumber;
    replaceOriginal = dlg.ReplaceOriginal;
}
```

- [ ] **Step 3: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 4: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit.UI/Dialogs.cs" "CadToolkit/src/CadToolkit/TextCommands.cs"
git -C "D:\Documents\GitHub\minis" commit -m "refactor(CadToolkit): move text numbering dialog to UI"
```

---

## Task 7: 阶段 3 - 统一版本号

**Files:**
- Modify: `CadToolkit/src/CadToolkit.Core/CadToolkit.Core.csproj`
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/autoload.lsp`
- Modify: `CadToolkit/build-all.bat`
- Modify: `.github/workflows/cadtoolkit.yml`

- [ ] **Step 1: 在 Core csproj 增加版本属性**

在第一个 `PropertyGroup` 加入：

```xml
<Version>1.22</Version>
<AssemblyVersion>1.22.0.0</AssemblyVersion>
<FileVersion>1.22.0.0</FileVersion>
```

- [ ] **Step 2: 在 `Config.cs` 读取程序集版本**

新增：

```csharp
public static string CurrentVersion
{
    get
    {
        try
        {
            var v = typeof(Config).Assembly.GetName().Version;
            if (v != null) return "v" + v.Major + "." + v.Minor;
        }
        catch { }
        return GetString("Version", "v1.22");
    }
}
```

把默认配置中的：

```csharp
sb.AppendLine("Version=v1.22");
```

替换为：

```csharp
sb.AppendLine("Version=" + CurrentVersion);
```

- [ ] **Step 3: build-all.bat 注入 autoload 版本**

在 bat 中增加：

```bat
set "CT_VERSION=v1.22"
```

复制 autoload 前，用 PowerShell 替换：

```bat
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content '%BASE%autoload.lsp' -Raw) -replace 'CadToolkit v[0-9.]+ ready', 'CadToolkit %CT_VERSION% ready' | Set-Content '%DEPLOY%\autoload.lsp' -Encoding UTF8"
```

- [ ] **Step 4: CI release 使用 tag 版本**

在 `.github/workflows/cadtoolkit.yml` 的 Package step 中已存在：

```powershell
$ver = $tag -replace '^ct-v',''
```

增加对 `autoload.lsp` 的 tag 版本替换：

```powershell
$autoload = Get-Content "${{ github.workspace }}\CadToolkit\autoload.lsp" -Raw
$autoload = $autoload -replace 'CadToolkit v[0-9.]+ ready', "CadToolkit v$ver ready"
Set-Content "$pkg\autoload.lsp" $autoload -Encoding UTF8
```

- [ ] **Step 5: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
CadToolkit v1.22 ready
```

- [ ] **Step 6: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit.Core" "CadToolkit/autoload.lsp" "CadToolkit/build-all.bat" ".github/workflows/cadtoolkit.yml"
git -C "D:\Documents\GitHub\minis" commit -m "chore(CadToolkit): centralize version handling"
```

---

## Task 8: 阶段 3 - 配置解析注释

**Files:**
- Modify: `CadToolkit/src/CadToolkit.Core/Config.cs`
- Modify: `CadToolkit/README.md`

- [ ] **Step 1: 为 `StripInlineComment` 增加注释**

在方法上方加入：

```csharp
// Inline comments are recognized only when # or ; appears at the start of
// a value token or after whitespace. Values that intentionally begin with
// # or ; are not supported by this simple INI parser.
```

- [ ] **Step 2: README 增加配置限制说明**

在配置文件说明段落加入：

```markdown
> 配置值中如需使用 `#` 或 `;`，不要让它们出现在值开头或空格之后。
> 当前 INI 解析器会把这种写法识别为行内注释。
```

- [ ] **Step 3: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 4: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit.Core/Config.cs" "CadToolkit/README.md"
git -C "D:\Documents\GitHub\minis" commit -m "docs(CadToolkit): clarify ini comment parsing"
```

---

## Task 9: 阶段 3 - 提取 PanelBuilder

**Files:**
- Modify: `CadToolkit/src/CadToolkit/Plugin.cs`
- Create: `CadToolkit/src/CadToolkit.UI/PanelBuilder.cs`
- Modify: `CadToolkit/src/CadToolkit.UI/CadToolkit.UI.csproj`

- [ ] **Step 1: 新建 `PanelAction`**

在 `PanelBuilder.cs` 中加入：

```csharp
namespace CadToolkit.UI
{
    public class PanelAction
    {
        public string Kind;
        public string CommandName;
    }
}
```

- [ ] **Step 2: 新建 `PanelBuilder.Show`**

在 `PanelBuilder.cs` 中实现：

```csharp
public static class PanelBuilder
{
    public static PanelAction Show(string title, string version, List<CommandGroup> groups)
    {
        PanelAction result = null;

        // 直接搬移 Plugin.cs 当前 ShowPanel 中从 groupCols 计算到 f.ShowDialog()
        // 结束的 WinForms 布局代码，保持按钮尺寸、瀑布流列布局、底部 + / - 按钮、
        // version 标签、滚动区域和取消按钮行为完全一致。
        //
        // 原来的 string action 改为 PanelAction result：
        // 命令按钮点击时设置 result = new PanelAction { Kind = "CMD", CommandName = cmdName };
        // + 按钮点击时设置 result = new PanelAction { Kind = "ADD" };
        // - 按钮点击时设置 result = new PanelAction { Kind = "MANAGE" };
        // 用户取消或直接关闭窗口时 result 保持为空。

        return result;
    }
}
```

实施时不要改变布局算法，只移动代码。`Config.GetCommandGroups()` 返回的 `CommandGroup` 已在 `CadToolkit.Core` 中定义，可直接引用。

- [ ] **Step 3: 更新 UI csproj**

把：

```xml
<Compile Include="Dialogs.cs" />
```

替换为：

```xml
<Compile Include="Dialogs.cs" />
<Compile Include="PanelBuilder.cs" />
```

- [ ] **Step 4: 简化 `ShowPanel()`**

把 `ShowPanel()` 中的窗口构建代码替换为：

```csharp
var action = PanelBuilder.Show("CadToolkit - " + PlatformName, Config.Version + " | WLUP", groups);
if (action == null) return;
if (action.Kind == "CMD")
{
    string cmdName = action.CommandName;
    System.EventHandler idle = null;
    idle = delegate(object sender, System.EventArgs ea)
    {
        try { CadApp.Idle -= idle; } catch {}
        CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(cmdName + " ", true, false, true);
    };
    CadApp.Idle += idle;
}
else if (action.Kind == "ADD")
{
    using (var dlg = new AddCommandDialog())
    {
        if (dlg.ShowDialog() == DialogResult.OK && dlg.CmdLabel != null && dlg.CmdLabel.Length > 0 && dlg.CmdName != null && dlg.CmdName.Length > 0)
            Config.SaveCommand(dlg.CmdLabel, dlg.CmdName);
    }
}
else if (action.Kind == "MANAGE")
{
    using (var dlg = new ManageCommandsDialog()) { dlg.ShowDialog(); }
}
```

- [ ] **Step 5: 编译验证**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 6: 提交**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" add "CadToolkit/src/CadToolkit/Plugin.cs" "CadToolkit/src/CadToolkit.UI"
git -C "D:\Documents\GitHub\minis" commit -m "refactor(CadToolkit): extract panel builder"
```

---

## Task 10: 最终验证与推送

**Files:**
- Verify: all modified files

- [ ] **Step 1: 查看提交序列**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" log --oneline --decorate --max-count=12
```

Expected:

```text
最新提交包含每个阶段的独立 commit
```

- [ ] **Step 2: 查看工作区**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" status --short --branch
```

Expected:

```text
## main...origin/main [ahead N]
```

- [ ] **Step 3: 最终构建**

Run:

```powershell
& "D:\Documents\GitHub\minis\CadToolkit\build-all.bat"
```

Expected:

```text
Done! Output: C:\CadToolkit
```

- [ ] **Step 4: 推送**

Run:

```powershell
git -C "D:\Documents\GitHub\minis" push origin main
```

Expected:

```text
main -> main
```

---

## Self-Review

- Spec coverage: 阶段 1、阶段 2、阶段 3 的所有设计项都有对应 Task。
- Placeholder scan: 未发现未决占位内容，所有任务都有明确文件、命令和验证方式。
- Type consistency: `RunWithUndo`、`GetSelectionOrAbort`、`TextNumberDialog`、`PanelBuilder.Show`、`PanelAction` 的名称在所有任务中保持一致。
- Risk coverage: 三平台 csproj 更新、Undo 与 Transaction 顺序、PanelBuilder 类可见性都已在任务中处理。
