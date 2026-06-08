# BlockBrowser 阶段版本说明

## 2026-06-09：1.25.2 clean refactor

当前分支：`codex/blockbrowser-clean`

当前提交：`16e3049 refactor(BlockBrowser): extract export request planning`

本阶段目标是把已测试正常的 BlockBrowser 版本从混合分支中拆出，并在不改变用户可见行为的前提下，整理代码结构，方便后续继续维护。

## 已完成内容

- 拆出干净分支，只保留 `BlockBrowser/` 相关变化。
- 保留 GstarCAD 2022、AutoCAD 2020、ZWCAD 2020 三套构建。
- 移除 AutoCAD 2026 项目文件。
- 保留 `BlockBrowser.default.ini` 模板，部署时不覆盖本地 `config.ini`。
- 保留原生分类横向滚动条，修复分类按钮下边框被遮挡的问题。
- 调整 BB 面板菜单顺序，把“新建分类”放到分类栏右侧。
- 修复空分类选择逻辑：添加到库和导入块可选空库，BB 面板浏览分类不显示空分类。
- 抽出以下纯逻辑服务，降低 `BlockBrowserForm.cs` 和 `BlockBrowserPlugin.cs` 的复杂度：
  - `BlockInfoStatusService`
  - `SettingsUpdateService`
  - `BlockDeletePlanService`
  - `BlockBrowserInfoService`
  - `BlockRenamePlanService`
  - `AddToLibraryRequestService`
  - `ExportBlockRequestService`

## 部署状态

部署目录：`C:\BlockBrowser`

部署产物：

- `C:\BlockBrowser\gcad\BlockBrowser.dll`
- `C:\BlockBrowser\acad\BlockBrowser.dll`
- `C:\BlockBrowser\zwcad\BlockBrowser.dll`
- `C:\BlockBrowser\autoload.lsp`
- `C:\BlockBrowser\BlockBrowser.default.ini`

部署策略：

- 现有 `C:\BlockBrowser\config.ini` 保留。
- 不向 `gcad`、`acad`、`zwcad` 子目录写入独立配置。
- 三个平台共用根目录配置。

## 验证状态

自动测试：

- `BlockBrowser/tests` 全量通过，当前共 28 个 PowerShell 测试脚本。

构建验证：

- GstarCAD 2022 构建通过。
- AutoCAD 2020 构建通过。
- ZWCAD 2020 构建通过。

已知环境提示：

- 构建时仍会出现 `.NETFramework v4.8 Targeting Pack` 警告。
- 只要 MSBuild 退出码为 0，且三套 DLL 输出成功，该警告暂按历史环境问题处理。

## 回退点

如果后续测试发现问题，优先回退到本阶段前已测试正常的部署版本，或从分支历史中定位以下提交：

- `60a2dd9 feat(BlockBrowser): isolate clean safe sync branch`
- `16e3049 refactor(BlockBrowser): extract export request planning`

回退时注意：

- 不覆盖 `C:\BlockBrowser\config.ini`。
- 如果 CAD 已打开，需要先关闭对应 CAD，否则 DLL 可能被占用。
