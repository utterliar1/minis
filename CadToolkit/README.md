# CadToolkit v1.2

AutoCAD / 中望CAD / 浩辰CAD 三平台通用插件工具箱。

一个命令 `CC` 呼出分组面板，16 个内置工具 + 无限自定义扩展，配置文件随插件一起迁移。

## 支持平台

| 平台 | 最低版本 | 状态 |
|------|---------|------|
| AutoCAD | 2020+ | ✅ |
| 中望 CAD (ZWCAD) | 2020+ | ✅ |
| 浩辰 CAD (GstarCAD) | 2022+ | ✅ |

## 安装

1. 将 `CadToolkit` 文件夹放到任意位置（推荐 `C:\CadToolkit`）
2. 在 CAD 命令行加载：
   ```
   (load "C:/CadToolkit/autoload.lsp")
   ```
3. 输入 `CC` 呼出工具面板

> 建议将 `autoload.lsp` 添加到 CAD 启动组，实现开机自动加载。

## 内置功能（16 个）

### 文字编辑（6 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 查找替换 | `CT_FINDREPLACE` | 在当前图纸中查找并替换文字内容，支持单行文字（DBText）、多行文字（MText）和块属性文字。可选是否忽略大小写。 |
| 文字对齐 | `CT_ALIGN` | 将选中的多个单行文字按指定方式对齐。支持左对齐/居中/右对齐，可选择第一个文字为基点或手动指定基点，支持自动行距或指定行距。对齐参数自动保存到配置文件。 |
| 加下划线 | `CT_UNDERLINE` | 将选中的单行文字转换为带下划线的多行文字（MText），保持原始内容和位置不变。 |
| 格式复制 | `CT_TEXTBRUSH` | 文字格式刷。先选择源文字（获取其图层、颜色、字高、样式），再选择目标文字，一键应用格式。 |
| 文字合并 | `CT_TEXTMERGE` | 将多个单行文字/多行文字合并为一个多行文字（MText），按从上到下排列。 |
| 文字编号 | `CT_TEXTNUMBER` | 为选中的文字添加顺序编号。支持"追加"模式（在原文字后加编号）和"替换"模式（用编号替换原文字）。可自定义起始编号和步长。 |

### 图层管理（5 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 图层归零 | `CT_SETLAYER0` | 将选中的所有对象移到图层 0，快速清理杂乱图层。 |
| 孤立图层 | `CT_ISOLAYER` | 选中一个对象，冻结除该对象所在图层以外的所有图层。再次执行可恢复之前冻结的图层（切换模式）。 |
| 按层选择 | `CT_SELECTBYLAYER` | 选中一个对象，自动选中当前图纸中同一图层的所有对象。 |
| 按色选择 | `CT_SELECTBYCOLOR` | 选中一个对象，自动选中当前图纸中同一颜色的所有对象。 |
| 按块选择 | `CT_SELECTBYBLOCK` | 选中一个块参照，自动选中当前图纸中所有同名块参照。 |

### 图块操作（2 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 重命名块 | `CT_RENAMEBLOCK` | 点选一个块参照，自动识别块名，在弹窗中输入新名称完成重命名。无需手动输入旧块名。 |
| 快捷建块 | `CT_QUICKBLOCK` | 选择对象 → 指定基点 → 自动创建块。块名使用配置的前缀 + 自动递增编号（如 BK001、BK002）。可在配置中设置是否删除原对象。 |

### 绘图标注（3 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 画中心线 | `CT_CENTERLINE` | 为选中的圆和矩形自动绘制中心线。使用 0 层、红色、CENTER 线型。中心线长度自动按图形尺寸的一定比例延伸。 |
| 快速标注 | `CT_QUICKDIM` | 选中对象后自动计算包围盒，一键创建水平和垂直两个方向的对齐标注。标注偏移量自动按图形比例计算。 |
| 递增复制 | `CT_INCCOPY` | 选中含数字编号的对象（如 A001），连续指定复制位置，每次复制自动递增编号（A002、A003...）。 |

## 自定义命令

除了内置命令，还可以添加任意 CAD 命令到面板：

**方式一：编辑配置文件**

编辑 `CadToolkit.ini` 的 `[Commands]` 段：

```ini
[Commands]
# 文字编辑
查找替换=CT_FINDREPLACE
文字对齐=CT_ALIGN
# ... 自定义命令
清理=PU
打印=PLT
图层管理=LAYMCOL
```

**方式二：面板操作**

- 点击面板底部 `+` 按钮添加新命令
- 点击 `-` 按钮管理（编辑/删除）已有命令

## 配置文件说明

配置文件 `CadToolkit.ini` 与插件放在同一目录，随插件一起迁移。

```ini
# CadToolkit 配置文件
Version=v1.2

# 快捷建块设置
QuickBlockPrefix=BK        # 块名前缀
DeleteOriginal=true         # 建块后删除原对象

# 文字下划线设置
KeepOriginal=false          # 转下划线时是否保留原文字

# 文字对齐设置（自动保存，无需手动编辑）
AlignHorz=0                 # 0=左对齐, 1=居中, 2=右对齐
AlignUseFirstBase=true      # true=第一个文字为基点, false=手动指定
AlignLineSpacing=0          # 行距，0=自动

# 命令列表
[Commands]
查找替换=CT_FINDREPLACE
...
```

## 目录结构

```
CadToolkit/
├── autoload.lsp            # CAD 自动加载脚本
├── CadToolkit.ini          # 配置文件（共享）
├── build-all.bat           # 编译部署脚本
├── acad/                   # AutoCAD 专用 DLL
│   ├── CadToolkit.dll
│   ├── CadToolkit.Core.dll
│   └── CadToolkit.UI.dll
├── zwcad/                  # 中望 CAD 专用 DLL
│   └── ...
├── gcad/                   # 浩辰 CAD 专用 DLL
│   └── ...
└── src/                    # 源代码
    ├── CadToolkit/         # 主插件（3 个 csproj 对应 3 个平台）
    ├── CadToolkit.Core/    # 核心配置
    └── CadToolkit.UI/      # 界面组件
```

## 编译

需要 .NET Framework 4.8 SDK 和 MSBuild v4.0：

```bat
build-all.bat
```

自动编译三个平台并部署到 `C:\CadToolkit\`。

## License

MIT