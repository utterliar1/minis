# BlockBrowser - 块浏览器

多CAD平台块浏览器插件，支持浩辰CAD、AutoCAD、中望CAD。

可视化浏览DWG块库，双击缩略图快速插入到当前图纸。

## 功能

- **块库浏览** - 按分类文件夹组织，缩略图网格展示
- **缩略图预读** - 自动读取DWG预览图标，支持几何渲染回退，缓存为PNG
- **快速插入** - 双击缩略图，设置比例/角度，自动提示插入点
- **块库管理** - 从当前图纸选择对象保存为块，或导出已有块到库
- **搜索过滤** - 实时搜索块名称，按分类筛选
- **多平台兼容** - 一份源码，条件编译生成三个平台DLL

## 安装

### 目录结构

    BlockBrowser/
    +-- config.ini              配置文件
    +-- autoload.lsp            自动加载脚本
    +-- gcad/
    |   +-- BlockBrowser.dll    浩辰CAD
    +-- acad/
    |   +-- BlockBrowser.dll    AutoCAD
    +-- zwcad/
    |   +-- BlockBrowser.dll    中望CAD
    +-- 我的常用块/              默认块库
        +-- 常用/
        +-- 电气/
        +-- 建筑/
        +-- 机械/
        +-- 标注/
        +-- 其他/

### 使用方式

1. 将整个 `BlockBrowser` 文件夹复制到任意位置（如 `C:\BlockBrowser`）
2. 在CAD中执行 `APPLOAD`，加载 `autoload.lsp`
3. 输入命令 `BB` 打开块浏览器

`autoload.lsp` 会自动检测CAD平台和插件目录，首次运行后记住路径。

### 自动加载（可选）

将 `autoload.lsp` 的加载语句添加到 `acaddoc.lsp` 或CAD的启动脚本中，即可每次启动自动加载。

## 命令

| 命令 | 说明 |
|------|------|
| `BB` | 打开块浏览器（别名：`KLLQ`） |
| `BBADD` | 将当前图纸中选择的对象保存为块到库 |
| `BBEXPORT` | 将当前图纸中的块导出到库 |
| `BBTHUMB` | 清除缩略图缓存 |
| `BBINFO` | 显示插件信息 |

## 配置

编辑 `config.ini` 修改块库路径：

    # 相对路径（相对于插件根目录）
    LibraryPath=我的常用块

    # 或绝对路径
    LibraryPath=D:\我的块库

也可在块浏览器界面点击 **设置** 按钮修改路径。

## 从源码构建

### 环境要求

- .NET Framework 4.8 SDK（浩辰CAD / 中望CAD）
- .NET 8 SDK（AutoCAD 2026）
- Visual Studio 或 MSBuild

### CAD SDK路径

在 `build-all.bat` 中修改各CAD的安装路径：

    set "GCAD_DIR=C:\Program Files\浩辰软件\浩辰CAD2022"
    set "ACAD_DIR=C:\Program Files\Autodesk\AutoCAD 2026"
    set "ZWCAD_DIR=C:\Program Files\ZWSOFT\ZWCAD 2022"

### 编译

    build-all.bat

输出在 `bin\Release\{gcad,acad,zwcad}\BlockBrowser.dll`。

## 技术说明

- **条件编译**: `#if GSTARCAD` / `#if AUTOCAD` / `#if ZWCAD` 区分平台API差异
- **缩略图策略**: PreviewIcon -> 几何渲染（递归展开BlockReference）-> 文字占位图
- **插入方式**: 模态对话框选块 -> 关闭后CAD原生提示插入点，无焦点闪烁
- **配置读取**: DLL加载时从 `config.ini` 读取，支持相对/绝对路径