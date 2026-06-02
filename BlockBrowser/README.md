# BlockBrowser - 块浏览器

多CAD平台块浏览器插件，支持浩辰CAD、AutoCAD、中望CAD。

可视化浏览DWG块库，双击缩略图快速插入到当前图纸。

## 功能

- **块库浏览** - 按分类文件夹组织，缩略图网格展示，支持搜索过滤
- **缩略图预览** - 自动读取DWG预览图标，支持几何渲染回退，磁盘+内存双缓存
- **分类缓存** - 切换分类瞬间显示，已访问分类的卡片不重建
- **快速插入** - 双击缩略图，设置比例/角度，CAD原生提示插入点，无焦点闪烁
- **添加到库** - 弹窗选分类输名称，CAD提示选基点和对象，保存为块
- **导出块** - 弹窗列出当前图纸所有块，选择后导出到库
- **记忆分类** - 再次打开时自动恢复上次选的分类

## 安装

### 目录结构

```
BlockBrowser/
├── config.ini              配置文件
├── autoload.lsp            自动加载脚本
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
```

### 使用方式

1. 将整个 BlockBrowser 文件夹复制到任意位置
2. 在CAD中执行 APPLOAD，加载 autoload.lsp
3. 输入命令 BB 打开块浏览器

autoload.lsp 会自动检测CAD平台和插件目录，首次运行后记住路径。

## 命令

| 命令 | 说明 |
|------|------|
| BB | 打开块浏览器（别名：KLLQ） |
| BBADD | 将选择的对象保存为块到库 |
| BBEXPORT | 从当前图纸导出块到库 |
| BBTHUMB | 清除缩略图缓存 |
| BBINFO | 显示插件信息 |

## 配置

编辑 config.ini 修改块库路径，支持相对路径和绝对路径。也可在界面点击设置按钮修改。

## 从源码构建

### 环境要求

- .NET Framework 4.8 SDK（浩辰CAD / 中望CAD）
- .NET 8 SDK（AutoCAD 2026）
- Visual Studio 或 MSBuild

### 编译

```
build-all.bat
```

输出在 bin/Release/{gcad,acad,zwcad}/BlockBrowser.dll

## 技术说明

- 条件编译: GSTARCAD / AUTOCAD / ZWCAD 区分平台API差异
- 缩略图策略: PreviewIcon -> 几何渲染 -> 文字占位图，磁盘PNG缓存+内存Bitmap缓存
- 分类缓存: Dictionary按分类缓存卡片实例，切换分类不重建，瞬间显示
- 插入方式: 模态对话框选块 -> 关闭后CAD原生提示插入点，无焦点闪烁
- 添加到库: 弹窗选分类/输名称 -> CAD提示选基点 -> 选择对象 -> 保存
- 导出块: 弹窗列出当前图纸所有块定义 -> 选择块和分类 -> 导出
- 配置读取: DLL加载时从config.ini读取，支持相对/绝对路径