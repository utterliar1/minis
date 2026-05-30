# jbot

自用 Telegram 机器人插件合集。

## emby-notify

新入库消息监控 → PushPlus 微信推送。

独立运行，不依赖 jbot 主程序，用自己的 Telethon 客户端监听指定 bot 的消息。

### 环境变量

| 变量 | 必填 | 说明 |
|------|------|------|
| `API_ID` | ✅ | Telegram API ID，去 https://my.telegram.org 获取 |
| `API_HASH` | ✅ | Telegram API Hash |
| `MONITOR_CHATS` | ✅ | 监听的 bot 用户名或 chat ID，多个逗号分隔 |
| `PUSHPLUS_TOKEN` | ✅ | PushPlus token，去 https://www.pushplus.plus 获取 |
| `PUSHPLUS_TOPIC` | ❌ | 群组编码，留空只推给自己 |
| `SESSION_NAME` | ❌ | Telethon 会话文件名，默认 `emby_notify` |

### 运行

```bash
pip install telethon httpx
python emby_notify.py
```

首次运行会要求输入手机号和验证码创建 session，之后自动登录。
