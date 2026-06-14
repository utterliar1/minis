# CadToolkit

AutoCAD / 中望CAD / 浩辰CAD 三平台通用插件工具箱。

一个命令 `CC` 呼出分组面板，20 个内置工具 + 无限自定义扩展，配置文件随插件一起迁移。

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

## 内置功能（20 个）

### 文字编辑（7 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 查找替换 | `CT_FINDREPLACE` | 在当前图纸中查找并替换文字内容，支持单行文字（DBText）、多行文字（MText）和块属性文字。可选是否忽略大小写。 |
| 文字对齐 | `CT_ALIGN` | 将选中的多个单行文字按指定方式对齐。支持左对齐/居中/右对齐，可选择第一个文字为基点或手动指定基点，支持自动行距或指定行距。对齐参数自动保存到配置文件。 |
| 加下划线 | `CT_UNDERLINE` | 将选中的单行文字转换为带下划线的多行文字（MText），保持原始内容和位置不变。 |
| 格式复制 | `CT_TEXTBRUSH` | 文字格式刷。先选择源文字（获取其图层、颜色、字高、样式），再选择目标文字，一键应用格式。 |
| 文字合并 | `CT_TEXTMERGE` | 将多个单行文字/多行文字合并为一个多行文字（MText），按从上到下排列。 |
| 文字编号 | `CT_TEXTNUMBER` | 为选中的文字添加顺序编号。可选择前缀、后缀或替换三种模式，并设置起始号。 |
| 文字规范 | `CT_TEXTSTYLESTANDARD` | 按配置的标准文字样式和别名规则扫描文字，预览后归并旧文字样式。 |

默认标准文字样式：

```ini
STANDARD-TEXT=gbenor.shx|gbcbig.shx|0|1.0|0
```

### 图层管理（5 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 图层归零 | `CT_SETLAYER0` | 将选中的所有对象移到图层 0，快速清理杂乱图层。 |
| 图层规范 | `CT_LAYERSTANDARD` | 按配置的标准图层和别名规则扫描当前空间，预览后创建/修正标准图层并迁移匹配对象。 |
| 孤立图层 | `CT_ISOLAYER` | 选中一个对象，冻结除该对象所在图层以外的所有图层。再次执行可恢复之前冻结的图层（切换模式）。 |
| 按层选择 | `CT_SELECTBYLAYER` | 先选源对象获取图层，再选择搜索范围；直接回车则搜索全图。 |
| 按色选择 | `CT_SELECTBYCOLOR` | 先选源对象获取颜色，再选择搜索范围；直接回车则搜索全图。 |

### 图块操作（4 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 重命名块 | `CT_RENAMEBLOCK` | 点选一个块参照，自动识别块名，在弹窗中输入新名称完成重命名。无需手动输入旧块名。 |
| 快捷建块 | `CT_QUICKBLOCK` | 选择对象 → 指定基点 → 自动创建块。块名使用配置的前缀 + 自动递增编号（如 BK001、BK002）。可在配置中设置是否删除原对象。 |
| 改块基点 | `CT_CHANGEBASEPOINT` | 选择普通块参照，指定新的块基点，插件更新同名块定义并补偿现有参照位置，使图面上的块图形保持不动。 |
| 按块选择 | `CT_SELECTBYBLOCK` | 先选源块参照获取块名，再选择搜索范围；直接回车则搜索全图。 |

### 绘图标注（4 个）

| 面板名称 | 命令 | 功能说明 |
|---------|------|---------|
| 画中心线 | `CT_CENTERLINE` | 为选中的圆和矩形自动绘制中心线。使用 0 层、红色、CENTER 线型。中心线长度自动按图形尺寸的一定比例延伸。 |
| 快速标注 | `CT_QUICKDIM` | 选中对象后自动计算包围盒，一键创建水平和垂直两个方向的对齐标注。标注偏移量自动按图形比例计算。 |
| 递增复制 | `CT_INCCOPY` | 选中含数字编号的对象（如 A001），连续指定复制位置，每次复制自动递增编号（A002、A003...）。 |
| Z轴归零 | `CT_FLATTEN` | 将选中对象的 Z 坐标尽量归零，适合清理二维图纸里的高度偏移。 |

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

配置文件统一放在 `CadToolkit` 根目录，三端共享同一份 `CadToolkit.ini`，修改一次即可对 AutoCAD / ZWCAD / GstarCAD 生效。

升级或重新部署时不要覆盖已有 `CadToolkit.ini`。发布包提供 `CadToolkit.default.ini` 作为默认模板；如果根目录没有 `CadToolkit.ini`，插件首次启动会自动生成一份。

如果已有 `CadToolkit.ini` 缺少新版新增的基础配置项，插件启动时会自动补上默认值；已有配置值、自定义 `[Commands]`、`[LayerStandard]` 和 `[LayerMap]` 不会被覆盖或合并。

> 配置值中如需使用 `#` 或 `;`，不要让它们出现在值开头或空格之后。当前 INI 解析器会把这种写法识别为行内注释。

```ini
# CadToolkit 配置文件

# 快捷建块设置
QuickBlockPrefix=BK
DeleteOriginal=true

# 文字下划线设置
KeepOriginal=false

# 文字对齐设置（自动保存，无需手动编辑）
AlignHorizontal=0
AlignUseFirstBase=true
AlignLineSpacing=0

# 图层管理设置
IsoLayerKeepLayer0=false

# 命令列表
[Commands]
查找替换=CT_FINDREPLACE
...
```

### 配置体检

运行 `CT_CONFIGCHECK` 或面板里的 `配置体检` 可以检查 `CadToolkit.ini` 是否缺少基础项、官方命令、必要 section，是否存在映射目标缺失或标准行格式错误。

自动修复只会补缺失基础项、补官方命令、重命名旧官方命令和清理已知错误注释；不会覆盖图层标准、图层映射、文字样式标准或文字样式映射。

也可以在 PowerShell 中运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File C:\CadToolkit\tools\check-config.ps1 -Fix
```

### 图层规范配置

图层规范由 `CadToolkit.ini` 控制。三端共用 `C:\CadToolkit\CadToolkit.ini`，修改后下一次执行 `CT_LAYERSTANDARD` 即可读取新配置。

执行逻辑：

1. 先按 `[LayerStandard]` 创建或修正标准图层的颜色、线型、线宽和打印状态。
2. 再按 `[LayerMap]` 识别旧图层。别名默认全字匹配，需要包含、前缀、后缀匹配时用 `*` 通配符。
3. 命中 `LayerStandardWhitelist` 的图层会保持原样，不参与规范化，也不会兜底归 0。
4. 执行前会弹出预览，列出“会迁移的图层”“未识别图层”“白名单图层”，可用顶部筛选按钮和关键词框按图层名过滤查看，也可以点“复制当前”复制当前视图，确认后才会修改图纸。

基础开关：

```ini
LayerStandardFallbackTo0=false
LayerStandardWhitelist=0,Defpoints,*图框*,*视口*,*原有*,*新增*
```

| 配置项 | 说明 |
|--------|------|
| `LayerStandardFallbackTo0` | 未识别图层是否归到 0 层。建议默认 `false`，需要集中清理时在预览窗口临时勾选。 |
| `LayerStandardWhitelist` | 全局白名单。支持精确匹配和 `*` 通配，多个规则用英文逗号分隔。 |

白名单写法示例：

| 写法 | 含义 |
|------|------|
| `0` | 只匹配名为 `0` 的图层 |
| `Defpoints` | 只匹配 `Defpoints` |
| `*图框*` | 图层名中包含“图框” |
| `*原有*` | 图层名中包含“原有” |
| `A-*` | 以 `A-` 开头 |
| `*-OLD` | 以 `-OLD` 结尾 |

`[LayerStandard]` 定义标准图层，格式为：

```ini
标准图层=颜色|线型|线宽|是否打印
```

字段说明：

| 字段 | 示例 | 说明 |
|------|------|------|
| 颜色 | `4`、`200` | CAD ACI 颜色号 |
| 线型 | `CONTINUOUS`、`CENTER`、`HIDDEN` | 线型名。插件会尝试从 CAD 自带 lin 文件加载缺失线型。 |
| 线宽 | `Default`、`0.25` | `Default` 表示默认线宽；数字按毫米写。 |
| 是否打印 | `true` / `false` | 是否参与打印 |

`[LayerMap]` 定义旧图层到标准图层的识别规则，格式为：

```ini
标准图层=别名1,别名2,别名3
```

别名默认全字匹配；需要包含、前缀、后缀匹配时用 `*` 通配符。例如 `0-4` 只匹配名为 `0-4` 的图层，`*风管*` 才会匹配 `风管-一层` 并归到 `5-风网`。

完整示例：

```ini
[LayerStandard]
0-设备层=4|CONTINUOUS|Default|true
1-中心线层=1|CENTER|Default|true
2-虚线层=4|HIDDEN|Default|true
3-文字层=3|CONTINUOUS|Default|true
4-标注层=3|CONTINUOUS|Default|true
10-非标=31|CONTINUOUS|Default|true

[LayerMap]
0-设备层=*设备*,0-4,*VIS*
1-中心线层=*中心*,*中心线*,*CENTER*,0-1,1,*AXIS*,*CLEARANCE*,ZX,ZXX
3-文字层=*文字*,*说明*,*编号*,*TEXT,*txt
4-标注层=*标注*,*尺寸*,*DIM*,*dim*
5-风网=*风网*,*风管*,*风道*,0-5,FW
```

使用建议：

- 新增标准图层时，必须同时在 `[LayerStandard]` 添加图层属性，并在 `[LayerMap]` 添加旧图层别名。
- 不确定是否该处理的图层，先加到白名单，预览确认后再决定是否移除。
- 第一次给同事使用时，建议保持 `LayerStandardFallbackTo0=false`，避免未识别图层被批量归 0。

## 目录结构

```
CadToolkit/
├── autoload.lsp            # CAD 自动加载脚本
├── CadToolkit.ini          # 用户配置文件（共享，升级时保留）
├── CadToolkit.default.ini  # 默认配置模板
├── build-all.bat           # 编译部署脚本
├── tools/
│   └── check-config.ps1    # 配置体检脚本
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
