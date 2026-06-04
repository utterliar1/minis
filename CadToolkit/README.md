# CadToolkit

AutoCAD / ZWCAD / GstarCAD 三平台通用插件工具箱。一个命令呼出面板，所有工具命令均可通过配置文件自定义。

## 支持平台

| 平台 | 版本 | 状态 |
|---------|---------|------|
| AutoCAD | 2020+ | ✅ |
| 中望 CAD | 2020+ | ✅ |
| 浩辰 CAD | 2022+ | ✅ |

## 快速开始

1. 将 `CadToolkit` 文件夹放到 `C:\` 或 `D:\` 根目录
2. 在 CAD 中加载 `autoload.lsp`：
   ```
   (load "C:/CadToolkit/autoload.lsp")
   ```
3. 输入 `CC` 呿出工具面板

## 命令

| 命令 | 说明 |
|---------|------|
| `CC` | 呿出工具面板 |
| `CT_FINDREPLACE` | 查找替换（支持单行文字/多行文字/块属性） |
| `CT_ALIGN` | 单行文字九宫格对齐 |
| `CT_UNDERLINE` | 单行文字转带下划线 MText |
| `CT_RENAMEBLOCK` | 重命名块（选中块后自动识别旧名） |
| `CT_QUICKBLOCK` | 选择对象快捷建块 |
| `CT_SETLAYER0` | 将选中对象改到 0 层 |

## 目录结构

```
CadToolkit/
├── autoload.lsp          # CAD 加载脚本
├── build-all.bat         # 编译部署脚本
├── CadToolkit.ini        # 配置文件
└── src/
    ├── CadToolkit/
    │   ├── Plugin.cs
    │   ├── CadToolkit.AutoCAD.csproj
    │   ├── CadToolkit.ZWCAD.csproj
    │   └── CadToolkit.GstarCAD.csproj
    ├── CadToolkit.Core/
    │   ├── Config.cs
    │   └── CadToolkit.Core.csproj
    └── CadToolkit.UI/
        ├── Dialogs.cs
        └── CadToolkit.UI.csproj
```

## 编译

需要 .NET Framework 4.8 和 MSBuild：

```bat
build-all.bat
```

编译产物自动部署到 `C:\CadToolkit\`。

## 自定义命令

编辑 `CadToolkit.ini` 的 `[Commands]` 段：

```ini
[Commands]
查找替换=CT_FINDREPLACE
文字对齐=CT_ALIGN
加下划线=CT_UNDERLINE
重命名块=CT_RENAMEBLOCK
快捷建块=CT_QUICKBLOCK
改到0层=CT_SETLAYER0
图层管理=LAYMCOL
清理=PU
```

也可以在面板中点击「添加」「管理」来增删命令。

## 配置项

```ini
Version=v1.1
QuickBlockPrefix=BK
DeleteOriginal=true
KeepOriginal=false
```

| 配置项 | 说明 | 默认值 |
|---------|------|------|
| `Version` | 版本号 | v1.0 |
| `QuickBlockPrefix` | 快捷建块前缀 | BK |
| `DeleteOriginal` | 建块后删除原对象 | true |
| `KeepOriginal` | 转换下划线时保留原文字 | false |

## 自动加载

将 `autoload.lsp` 添加到 CAD 启动组，启动时自动加载插件，直接输入 `CC` 即可使用。
