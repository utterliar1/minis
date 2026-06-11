# 块浏览器 BlockBrowser

多 CAD 平台块浏览器插件，支持**浩辰CAD 2022**、**AutoCAD 2020**、**中望CAD 2020**。

可视化浏览 DWG 块库，双击缩略图快速插入到当前图纸。

## 功能

- **块库浏览** — 按分类文件夹组织，缩略图网格展示
- **缩略图预览** — 自动读取 DWG 预览图标，支持几何渲染回退，缓存为 PNG
- **快速插入** — 双击缩略图，按设置的比例/角度自动提示插入点
- **块库管理** — 从当前图纸选择对象保存为块，或导出已有块到库（支持多选）
- **搜索过滤** — 实时搜索块名称，按分类筛选
- **窗口记忆** — 窗口大小、图标大小、上次分类自动保存
- **多平台兼容** — 一份源码，条件编译生成三个平台 DLL

## 安装

### 目录结构

    BlockBrowser/
    ├── config.ini              配置文件
    ├── autoload.lsp            自动加载脚本
    ├── build-all.bat           一键编译
    ├── gcad/
    │   └── BlockBrowser.dll    浩辰CAD
    ├── acad/
    │   └── BlockBrowser.dll    AutoCAD
    ├── zwcad/
    │   └── BlockBrowser.dll    中望CAD
    └── 我的常用块/              默认块库
        ├── 常用/
        ├── 电气/
        ├── 建筑/
        ├── 机械/
        ├── 标注/
        └── 其他/

### 使用方式

1. 将整个 BlockBrowser 文件夹复制到任意位置（如 `C:\BlockBrowser`）
2. 在 CAD 中执行 APPLOAD，加载 autoload.lsp
3. 输入命令 BB 打开块浏览器

autoload.lsp 会自动检测 CAD 平台和插件目录。

### 自动加载（可选）

将 autoload.lsp 的加载语句添加到 acaddoc.lsp 或 CAD 的启动脚本中，即可每次启动自动加载。

## 命令

| 命令 | 说明 |
|------|------|
| BB | 打开块浏览器（别名：KLLQ） |
| BBADD | 将当前图纸中选择的对象保存为块到库 |
| BBEXPORT | 将当前图纸中的块导出到库（支持多选） |
| BBMIRROR | 从 NAS 更新本地图库 |
| BBSYNC | 将本地新增块安全同步到 NAS，并报告冲突 |
| BBTHUMB | 清除缩略图缓存 |
| BBINFO | 显示插件信息 |

## 阶段资料

- [阶段版本说明](RELEASE_NOTES.md)
- [手动测试清单](MANUAL_TEST_CHECKLIST.md)

## 界面操作

- **插入** — 选中块后点击插入，或双击缩略图
- **删除** — 选中块后删除（需确认）
- **重命名** — 选中块后重命名文件
- **添加到库** — 关闭对话框后在 CAD 中选择对象、指定基点、输入名称
- **导出块** — 从当前图纸批量导出块到库（支持 Ctrl/Shift 多选）
- **设置** — 修改块库路径、插入比例/角度
- **搜索** — 实时过滤块名称（支持模糊匹配）
- **大小** — 切换缩略图尺寸（小/中/大/特大）

## 配置

编辑 config.ini：

    # 当前实际使用的块库路径（通常由程序按模式自动更新）
    LibraryPath=我的常用块
    NasLibraryPath=我的常用块
    LocalMirrorPath=我的常用块
    CurrentLibraryMode=Local

    # 缩略图大小
    ThumbSize=128

    # 插入比例和角度
    InsertScale=1
    InsertRotation=0

    # 窗口大小（自动保存）
    FormWidth=1000
    FormHeight=650

也可在块浏览器界面点击 **设置** 按钮修改。

## NAS 与便携同步

BlockBrowser 支持办公室 NAS 主图库和移动电脑本地副本。推荐配置：

    LibraryPath=\\NAS\CADBlocks\BlockBrowser
    NasLibraryPath=\\NAS\CADBlocks\BlockBrowser
    LocalMirrorPath=我的常用块
    PreferLocalWhenNasUnavailable=1
    AllowNasSync=0
    CurrentLibraryMode=Auto
    UserName=WLUP

在 `Auto` 模式下，NAS 可用时使用 NAS 主图库；NAS 不可用且本地副本存在时，自动使用本地副本，并把离线新增、重命名、删除请求记录到 `.blockbrowser/local-changes.json`。

默认新安装使用 `CurrentLibraryMode=Local`，也就是直接使用插件目录下的 `我的常用块`。需要接入办公室 NAS 时，只要在设置里填写 `NAS 主图库路径` 并切换到 `Auto` 即可；修改本地副本路径不会改动 NAS 主图库路径。

同步策略默认保护 NAS：

- 本地新增文件且 NAS 没有同路径文件时，允许上传。
- NAS 已有同路径文件时，跳过并报告重复。
- 本地编辑过的 DWG 不会静默覆盖 NAS。
- 本地删除会记录为删除请求，不会自动删除 NAS 文件。
- 缩略图缓存仍保留在每台电脑本地，不共享到 NAS。

常用操作：

- 普通同事日常只需要点击面板上的 **更新本地图库**，或运行 `BBMIRROR` 从 NAS 更新本地图库。有未同步本地修改时会拒绝更新，避免覆盖移动电脑上的改动。
- `BBSYNC` 和同步中心只给指定维护人使用。普通电脑保持 `AllowNasSync=0`，维护人电脑改为 `AllowNasSync=1` 后才会显示同步入口并允许执行同步。
- `BBSYNC`：上传安全的新文件，并汇总重复、冲突、删除待确认和失败数量。
- 面板「图库 > 同步中心」：同步前查看完整明细，确认后执行；执行记录会追加到本地副本 `.blockbrowser\sync-log.txt`，方便回看或发给同事排查。

## 缩略图逻辑

1. **缓存** — 读取 `.thumbs/` 目录下的 PNG 缓存（按文件路径+大小+修改时间校验有效性）
2. **PreviewIcon** — 读取 CAD 引擎的块预览图标（验证非空白）
3. **几何渲染** — 优先渲染模型空间（添加到库的文件），其次渲染最大块定义（导出块的文件）
4. **占位符** — 以上均失败时显示文件名占位图

清除缓存：在 CAD 中输入 BBTHUMB。

## 从源码构建

### 环境要求

- .NET Framework 4.8 SDK
- Visual Studio 或 MSBuild

### CAD SDK 路径

在 build-all.bat 中修改各 CAD 的安装路径：

    set "GCAD_DIR=C:\Program Files\浩辰软件\浩辰CAD2022"
    set "ACAD_DIR=C:\Program Files\Autodesk\AutoCAD 2020"
    set "ZWCAD_DIR=C:\Program Files\ZWSOFT\ZWCAD 2020"

### 编译

    build-all.bat

输出在 `bin\Release\{gcad,acad,zwcad}\BlockBrowser.dll`。

## 技术说明

- **条件编译**：`#if GSTARCAD` / `#if AUTOCAD` / `#if ZWCAD` 区分平台 API
- **缩略图**：PreviewIcon → 模型空间渲染 → 最大块定义渲染 → 占位图，PNG 磁盘缓存
- **插入**：模态对话框选块 → 关闭后 CAD 原生提示插入点
- **配置**：DLL 加载时从 config.ini 读取，支持相对/绝对路径
- **版本号**：`BlockLibrary.AppVersion` 唯一定义，其他位置读取
