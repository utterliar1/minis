# jbot

自用 Telegram 机器人插件合集。

---

## emby-notify

新入库消息监控 → PushPlus 微信推送。

监听指定 TG bot 收到的新入库消息，自动解析媒体信息，通过 PushPlus 推送到微信。

### 配置

在 `diybotset.json` 中添加：

```json
"monitor_chats": "@your_bot_username",
"pushplus_token": "你的pushplus_token",
"pushplus_topic": ""
```

| 字段 | 必填 | 说明 |
|------|------|------|
| `monitor_chats` | ✅ | 接收入库消息的 bot 用户名或 chat ID，多个逗号分隔 |
| `pushplus_token` | ✅ | PushPlus 推送 token，去 https://www.pushplus.plus 获取 |
| `pushplus_topic` | ❌ | 群组编码，留空只推给自己 |

### 使用

1. 将 `emby_notify.py` 放入 jbot 的 `maid/` 目录
2. 在 `diybotset.json` 中添加上述配置
3. 重启 jbot（`pm2 restart jbot`）

### 依赖

- `telethon`（jbot 已有）
- `httpx`（jbot 已有）

无需额外安装。

---

## sticker

贴纸管理工具，支持静态/动态贴纸转图片/GIF、添加到贴纸包、批量处理等。

### 功能

| 命令 | 说明 |
|------|------|
| `pic` | 回复贴纸发送，静态贴纸转 PNG，动态贴纸转 GIF |
| `pic n` | 以文件形式发送（不压缩） |
| `s` | 回复图片/贴纸，添加到贴纸包 |
| `s <emoji>` | 指定 emoji |
| `s png` | 不转圆角 |
| `s merge` | 批量添加（回复第一条消息后使用） |
| `s set_round` | 开关自动圆角 |
| `s to <pack_name>` | 添加到指定贴纸包 |

### 依赖

动态贴纸转 GIF 需要额外安装：

```bash
pip install lottie
```

其他依赖（`telethon`、`Pillow`、`httpx`）jbot 已有。
