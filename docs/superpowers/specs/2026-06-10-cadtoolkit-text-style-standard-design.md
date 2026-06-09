# CadToolkit 文字样式规范设计

## 目标

新增 `CT_TEXTSTYLESTANDARD`，做成类似 `CT_LAYERSTANDARD` 的配置驱动工具：先扫描文字样式，预览将归并、未识别、白名单，再由用户确认执行。功能一步到位，但默认安全。

## 配置

根配置新增：

```ini
TextStyleFallbackToStandard=false
TextStyleFallbackStyle=STANDARD-TEXT
TextStyleWhitelist=Standard,Annotative,*DIM*
TextStyleNormalizeHeight=false
TextStyleNormalizeWidthFactor=false
TextStyleNormalizeOblique=false
TextStyleNormalizeColorByLayer=false
TextStyleDeleteUnusedOldStyles=false
```

新增标准样式段：

```ini
[TextStyleStandard]
STANDARD-TEXT=仿宋_GB2312||0|1.0|0
TITLE-TEXT=黑体||0|1.0|0
```

格式为：

```text
标准样式=字体|大字体|固定字高|宽度因子|倾斜角
```

新增映射段：

```ini
[TextStyleMap]
STANDARD-TEXT=Standard,txt,*宋体*,HZTXT
TITLE-TEXT=*标题*,TITLE
```

匹配方式与图层规范一致：默认全字匹配，只有包含 `*` 的模式使用通配匹配。

## 扫描范围

预览窗口提供勾选项：

- `处理当前空间文字`：默认选中。
- `处理块属性文字`：默认不选。
- `处理块定义内部文字`：默认不选。

普通文字包括 `DBText` 和 `MText`。块属性文字是块参照上的 `AttributeReference`。块定义内部文字是普通块定义内的 `DBText`、`MText`、`AttributeDefinition`。

块定义内部文字会影响所有引用该块定义的块实例，所以默认不选。

## 规范动作

执行时先创建或修正 `[TextStyleStandard]` 中定义的标准文字样式，再把匹配到的文字对象 `TextStyleId` 改为目标标准样式。

可选规范对象属性：

- 统一字高：把 `DBText.Height`、`MText.TextHeight`、`AttributeReference.Height`、`AttributeDefinition.Height` 改为标准样式固定字高。标准样式固定字高为 `0` 时不改对象字高。
- 统一宽度因子：把支持该属性的文字对象改为标准样式宽度因子。
- 统一倾斜角：把支持该属性的文字对象改为标准样式倾斜角。
- 颜色 ByLayer：把文字对象颜色改成 ByLayer。

这些动作默认关闭，只在预览窗口勾选后执行。

## 兜底与清理

未识别样式默认保持原样。勾选 `未识别样式归默认` 后，未识别且不在白名单的文字会归并到 `TextStyleFallbackStyle`。

`TextStyleDeleteUnusedOldStyles` 默认关闭。勾选清理时，只删除规范后不再被任何对象引用、且不是标准样式、不是白名单、不是 `Standard` 的旧文字样式。删除失败只记录并提示数量，不中断已完成的规范动作。

## 预览

预览窗口沿用图层规范体验：

- 顶部筛选：`全部 / 未识别 / 将归并 / 白名单`
- 关键词搜索
- `复制当前`
- 聚焦筛选或搜索时自动展开
- 勾选项变化后重新扫描并重建预览

摘要显示标准样式数量、将归并样式数/对象数、未识别样式数/对象数、白名单样式数/对象数。

执行按钮始终按完整计划执行，不只执行当前筛选/搜索结果。

## 文档与命令

面板命令名：

```ini
文字样式规范=CT_TEXTSTYLESTANDARD
```

同步更新：

- `CadToolkit/CadToolkit.ini`
- `CadToolkit/CadToolkit.default.ini`
- `CadToolkit/src/CadToolkit.Core/Config.cs`
- `CadToolkit/README.md`
- `CadToolkit/CadToolkit使用手册.html`

已有用户配置只补缺失的根配置和官方命令，不覆盖 `[TextStyleStandard]`、`[TextStyleMap]` 或用户已有命令。

## 测试

测试覆盖：

- 配置默认值、模板、内置默认文本和文档同步。
- 新命令注册和面板命令同步。
- 样式映射默认全字匹配，通配符才包含匹配。
- 预览树包含摘要、未识别、将归并、白名单。
- 筛选、搜索、复制当前和聚焦展开逻辑存在。
- 默认范围只处理当前空间文字，块属性和块定义内部文字默认不选。
- 执行路径创建/修正标准样式、修改 `TextStyleId`、可选属性规范、可选清理旧样式。

## 自检

- 范围完整覆盖用户要求的“一步到位”。
- 默认行为安全：不处理块属性、不处理块定义、不兜底、不清理旧样式、不改对象属性。
- 配置升级策略保持不覆盖用户本地配置。
- 没有占位项。
