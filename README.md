# minis

自用脚本与工具合集。

## 目录结构

```
minis/
├── BlockBrowser/               CAD 块浏览器插件
│   ├── autoload.lsp            自动加载脚本
│   ├── config.ini              配置文件
│   ├── build-all.bat           一键编译
│   ├── BlockBrowserPlugin.cs   核心逻辑
│   ├── BlockBrowserForm.cs     界面
│   ├── BlockThumbnailCard.cs   缩略图控件
│   ├── gcad/acad/zwcad/        三平台 DLL
│   └── 我的常用块/              默认块库
├── CadToolkit/                 CAD 工具箱插件 (Ribbon UI)
│   ├── autoload.lsp            自动加载脚本
│   ├── CadToolkit.ini          配置文件
│   ├── build-all.bat           一键编译
│   └── src/                    源码 (Core + UI + 各平台)
├── nodeseek-checkin/           NodeSeek 签到
├── jbot/                       Telegram 机器人插件
│   ├── diy/xmsport.py          /xiaomi 小米运动刷步数
│   └── maid/                   功能插件
└── xmSport/                    小米运动刷步数
```

## 快速使用

### 块浏览器 BlockBrowser

多 CAD 平台可视化块浏览器插件，支持浩辰CAD / AutoCAD / 中望CAD。
详见 [BlockBrowser/README.md](BlockBrowser/README.md)

### CadToolkit

多 CAD 平台 Ribbon 工具箱插件，支持浩辰CAD / AutoCAD / 中望CAD。
详见 [CadToolkit/README.md](CadToolkit/README.md)

### 其他

- **nodeseek-checkin** — NodeSeek 论坛自动签到（Python + curl_cffi）
- **xmSport** — 小米运动刷步数（青龙面板）
- **jbot** — Telegram 机器人插件（Emby 推送、贴纸管理等）
