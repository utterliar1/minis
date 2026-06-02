# minis

自用脚本合集。

## 目录结构

```
minis/
  BlockBrowser/             CAD 块浏览器插件
    autoload.lsp            自动加载脚本
    config.ini              配置文件
    build-all.bat           一键编译
    BlockBrowserPlugin.cs   核心逻辑
    BlockBrowserForm.cs     界面
    BlockThumbnailCard.cs   缩略图控件
    gcad/acad/zwcad/        三平台 DLL
    我的常用块/              默认块库
    README.md               详见插件文档
  jbot/                     Telegram 机器人插件
    diy/                    自定义命令
      xmsport.py            /xiaomi 小米运动刷步数
    maid/                   功能插件
      emby_notify.py        Emby 新入库推送
      sticker.py            贴纸管理工具
  nodeseek-checkin/         NodeSeek 签到
  xmSport/                  小米运动刷步数
    xmsport.py              刷步数脚本 (青龙面板)
    data.txt                步数数据模板
```

## 快速使用

### 块浏览器 BlockBrowser

多 CAD 平台 (浩辰/AutoCAD/中望) 可视化块浏览器插件。
详见 [BlockBrowser/README.md](BlockBrowser/README.md)

### 小米运动刷步数

详见 [xmSport/README.md](xmSport/README.md)

### jbot 插件

详见 [jbot/README.md](jbot/README.md)
