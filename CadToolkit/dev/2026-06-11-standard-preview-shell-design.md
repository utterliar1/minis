# CadToolkit Standard Preview Shell Design

## Goal

图层规范和文字规范已经形成相同的预览交互：顶部筛选、搜索、树状预览、复制当前、执行和取消。后续颜色规范、线型规范、线宽规范也会需要类似体验。

本次目标是抽出一个可复用的预览窗口雏形，减少重复布局和树搜索代码，同时保持图层规范、文字规范现有业务行为不变。

## Scope

本轮抽取稳定共同部分：

- 统一创建固定尺寸的规范预览窗口。
- 统一创建顶部筛选栏：全部、未识别、迁移或归并、白名单、搜索。
- 统一创建 TreeView 基础样式和布局。
- 统一创建底部复制当前、执行、取消按钮。
- 统一 TreeNode 搜索、无匹配结果、报告格式化、展开逻辑。

本轮不抽取业务部分：

- 不抽取图层规范的图层匹配、迁移、清理逻辑。
- 不抽取文字规范的文字样式匹配、处理范围、外观同步、风险确认逻辑。
- 不设计完整的颜色规范或线型规范 API。

## Proposed Shape

新增一个轻量 helper，建议放在 `CadToolkit/src/CadToolkit/StandardPreviewUi.cs`，仍属于 `CadCommands` partial class 或同 namespace 内部静态类，避免引入新的程序集边界。

建议包含：

- `StandardPreviewFilterControls`：保存四个筛选按钮、搜索框和搜索标签。
- `CreateStandardPreviewForm(title, treeHeight)`：创建基础 Form。
- `CreateStandardPreviewFilterControls(migrationLabel)`：创建顶部筛选和搜索控件。
- `CreateStandardPreviewTree(height)`：创建 TreeView。
- `CreateStandardPreviewButtons(...)`：创建复制、执行、取消按钮。
- `FilterStandardPreviewNodes(...)`：复用搜索克隆、无匹配结果逻辑。
- `FormatStandardPreviewTreeReport(...)`：复用树报告格式化。
- `UpdateStandardPreviewTree(...)`：清空、添加节点，并在筛选或搜索时展开。

图层规范和文字规范各自仍负责：

- 生成完整 TreeNode 数组。
- 将自身枚举筛选状态映射到通用筛选按钮。
- 根据自身选项刷新预览。
- 执行实际规范化逻辑。

## Compatibility

现有用户可见行为应保持一致：

- 图层规范窗口标题、尺寸、筛选文字、搜索框、选项区、按钮位置不变。
- 文字规范窗口标题、尺寸、筛选文字、搜索框、选项区、按钮位置不变。
- 搜索命中、搜索无结果、复制当前视图、筛选后展开树的行为不变。

这次重构不改配置文件、不改命令名、不改 README 和手册。

## Testing

采用 TDD：

- 先补测试确认新的 helper 文件存在，并且图层规范、文字规范源码都调用它。
- 先补测试确认搜索树通用 helper 保留无匹配结果行为。
- 修改实现后运行：
  - `CadToolkit/tests/LayerStandardMatching.Tests.ps1`
  - `CadToolkit/tests/TextStyleStandard.Tests.ps1`
  - 全部 `CadToolkit/tests/*.Tests.ps1`
  - `git diff --check`

## Risks

- 过度抽象会让文字规范的刷新逻辑变难读。本设计只抽 UI 和树工具，不抽业务流程。
- WinForms 绝对坐标容易被 helper 隐式改变。本设计要求 helper 保持现有坐标和尺寸，测试继续检查关键源码特征。
- 图层规范使用 Unicode escape，文字规范使用中文 literal。实现时优先保持各文件现有风格，helper 内部可使用中文 literal。

