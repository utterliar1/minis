# minis

自用脚本合集，配合青龙面板和 Telegram 机器人使用。

## 目录结构

```
minis/
├── jbot/              # Telegram 机器人插件
│   ├── diy/           # 自定义命令
│   │   └── xmsport.py # /xiaomi - 小米运动刷步数
│   └── maid/          # 功能插件
│       ├── emby_notify.py  # Emby 新入库推送
│       └── sticker.py      # 贴纸管理工具
├── nodeseek-checkin/  # NodeSeek 签到
└── xmSport/           # 小米运动刷步数
    ├── xmsport.py     # 刷步数脚本（青龙面板）
    └── data.txt       # 步数数据模板
```

## 快速使用

### 小米运动刷步数

详见 [xmSport/README.md](xmSport/README.md)

### jbot 插件

详见 [jbot/README.md](jbot/README.md)
