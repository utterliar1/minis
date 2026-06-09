# minis

自用脚本、CAD 插件和自动化工具合集。

## 项目索引

| 目录 | 内容 | 入口 |
|------|------|------|
| `CadToolkit/` | AutoCAD / 中望 CAD / 浩辰 CAD 三平台通用工具箱插件。一个 `CC` 命令呼出分组面板，包含文字、图层、图块、绘图标注等工具。 | [CadToolkit/README.md](CadToolkit/README.md) |
| `BlockBrowser/` | 多 CAD 平台可视化块浏览器插件，包含块库、缩略图和三平台 DLL。 | [BlockBrowser/README.md](BlockBrowser/README.md) |
| `nodeseek-checkin/` | NodeSeek 论坛自动签到脚本。 | `nodeseek-checkin/` |
| `xmSport/` | 小米运动刷步数相关脚本。 | `xmSport/` |
| `jbot/` | Telegram 机器人插件与扩展脚本。 | `jbot/` |

## CadToolkit

CadToolkit 是当前主要维护的 CAD 工具箱项目：

- 三平台部署目录：`C:\CadToolkit`
- 共享配置文件：`C:\CadToolkit\CadToolkit.ini`
- 自动加载入口：`C:\CadToolkit\autoload.lsp`
- 一键构建脚本：[CadToolkit/build-all.bat](CadToolkit/build-all.bat)

## 目录结构

```text
minis/
├── BlockBrowser/          CAD 块浏览器插件
├── CadToolkit/            CAD 工具箱插件
│   ├── autoload.lsp       自动加载脚本
│   ├── CadToolkit.ini     共享配置文件模板
│   ├── build-all.bat      一键构建部署脚本
│   └── src/               插件源码
├── jbot/                  Telegram 机器人插件
├── nodeseek-checkin/      NodeSeek 签到脚本
└── xmSport/               小米运动脚本
```
