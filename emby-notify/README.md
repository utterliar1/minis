# emby-notify

Telegram 新入库消息监控 → PushPlus 微信推送

## 使用方式

将 `emby_notify.py` 复制到 `\\192.168.123.109\ql\jbot\maid\` 目录，重启 jbot 即可。

## 环境变量

| 变量 | 必填 | 说明 |
|------|------|------|
| `PUSHPLUS_TOKEN` | ✅ | PushPlus 推送 token，去 https://www.pushplus.plus 获取 |
| `PUSHPLUS_TOPIC` | ❌ | 群组编码，留空则只推给自己 |

## 工作原理

1. 监听所有发给 bot 的新消息
2. 匹配含"新入库"的消息，正则解析标题/评分/简介/链接等
3. 构造 HTML 模板，通过 PushPlus 推送到微信
