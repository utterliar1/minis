# CadToolkit 重构设计

**日期：** 2026-06-07
**状态：** 已批准
**范围：** CadToolkit 插件代码质量与可维护性提升

## 问题

`Plugin.cs` 有 1710 行，19 个命令方法、辅助方法、UI 构建和对话框逻辑全部堆在一个文件里。存在 21 处静默 `catch { }`、批量操作没有 Undo 组、12 个命令重复相同的选择模式、UI 构建代码散落各处。版本号在 3 个地方硬编码。

## 分阶段方案

### 阶段 1：零风险即时收益

**Undo 组** — 为 5 个批量命令添加撤销组：
- `CT_LAYERSTANDARD`
- `CT_ALIGN`
- `CT_QUICKBLOCK`
- `CT_SETLAYER0`
- `CT_TEXTMERGE`

在每个命令外层包裹 `Db.StartUndoMark()` / `Db.EndUndoMark()`，让用户 Ctrl+Z 一次回退整个操作。

**异常处理改进：**
将静默 `catch { }` 替换为分级处理：
- 关键路径（LayerStandard、QuickBlock、CenterLine）：`catch (Exception ex) { Ed.WriteMessage("\n警告：" + ex.Message); }`
- 配置读写：`catch (Exception ex) { Log("配置：" + ex.Message); }`
- 非关键操作（EnsureLineType、ApplyLayerRule 属性设置）：保留静默但记录日志

### 阶段 2：结构重组

**文件拆分** — 使用 `partial class CadCommands`：

| 文件 | 内容 | 预估行数 |
|------|------|---------|
| `Plugin.cs` | 入口、EnsureInit、公共辅助方法（CheckDoc、GetPendingOrSelection、Log、SimpleWildcardMatch、IsLayerWhitelisted、UiScale 缓存） | ~200 |
| `TextCommands.cs` | CT_FINDREPLACE、CT_ALIGN、CT_UNDERLINE、CT_TEXTBRUSH、CT_TEXTMERGE、CT_TEXTNUMBER | ~400 |
| `LayerCommands.cs` | CT_SETLAYER0、CT_LAYERSTANDARD、CT_ISOLAYER、CT_SELECTBYLAYER、CT_SELECTBYCOLOR | ~350 |
| `BlockCommands.cs` | CT_RENAMEBLOCK、CT_QUICKBLOCK、CT_SELECTBYBLOCK | ~200 |
| `DrawCommands.cs` | CT_CENTERLINE、CT_QUICKDIM、CT_INCCOPY、CT_FLATTEN | ~350 |

**重复模式提取：**
提取 `GetSelectionOrAbort()` 方法，消除 12 处重复的选择初始化代码。

**UiScale 缓存：**
缓存 DPI 系数，替代 33 次 `Graphics.FromHwnd(IntPtr.Zero)` 调用。

**TextNumber 对话框：**
将 Plugin.cs 中的内联对话框代码移到 `CadToolkit.UI/Dialogs.cs`，命名为 `TextNumberDialog`。

### 阶段 3：收尾打磨

**版本号统一：**
从 csproj 的 `AssemblyVersion` 属性读取版本号。通过 CI 或 build-all.bat 在构建时注入到 autoload.lsp 输出和 INI 默认配置中。

**配置文档：**
为 `StripInlineComment` 行为添加注释，说明限制（值不能以 `#` 或 `;` 开头）。

**PanelBuilder：**
将 ShowPanel 布局逻辑（~180 行）提取到 `CadToolkit.UI/PanelBuilder.cs`。

## 验证方式

- 阶段 1：在 CAD 中加载插件，运行 CT_LAYERSTANDARD + CT_ALIGN，验证 Ctrl+Z 可一次回退整个操作。临时重命名 CadToolkit.ini 测试错误路径。
- 阶段 2：运行 build-all.bat 编译全部 3 个平台。验证 18 个命令均可正常加载和执行。检查 partial class 编译无报错。
- 阶段 3：验证 `CC` 命令显示正确版本号。验证 autoload.lsp 加载时输出正确版本。

## 风险

- 文件拆分需要更新 3 个 csproj 文件以包含新的 .cs 文件
- Undo 标记与 Transaction 嵌套需要注意：StartUndoMark 必须在事务之前，EndUndoMark 在提交之后
- PanelBuilder 提取可能影响 GstarCAD 的 IExtensionApplication 注册（类可见性变化）
