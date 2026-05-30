# emby-notify

Telegram 新入库消息监控 → PushPlus 微信推送

监控 TG 机器人收到的新入库消息，自动解析媒体信息（标题、评分、简介、链接等），通过 PushPlus 推送到微信。

## 前置条件

1. **Telegram API 凭据** - 前往 https://my.telegram.org 获取 `api_id` 和 `api_hash`
2. **PushPlus Token** - 前往 https://www.pushplus.plus 获取推送 token
3. **Python 3.10+**

## 配置

```bash
cp config.json.example config.json
```

编辑 `config.json`：

| 字段 | 说明 |
|------|------|
| `api_id` | Telegram API ID |
| `api_hash` | Telegram API Hash |
| `session_name` | Telethon 会话文件名，默认 `emby_notify` |
| `monitor_chats` | 要监听的 chat 列表（bot 用户名或 chat ID） |
| `pushplus_token` | PushPlus 推送 token |
| `pushplus_topic` | PushPlus 群组编码（可选，留空则只推给自己） |
| `include_cover` | 是否在推送中包含影视封面图片 |

### monitor_chats 示例

```json
{
    "monitor_chats": ["your_bot_username", -1001234567890]
}
```

- 使用 bot 用户名：`"your_bot_username"`
- 使用 chat ID（数字）：`-1001234567890`

## 运行

```bash
pip install -r requirements.txt
python main.py
```

首次运行会要求输入 Telegram 手机号和验证码以创建 session 文件，之后无需重复验证。

## 后台运行

```bash
# Linux / macOS
nohup python main.py > emby-notify.log 2>&1 &

# 或使用 screen / tmux
screen -S emby-notify
python main.py
```

## 推送效果

收到新入库消息后，微信会收到一条格式化通知，包含：

- 📺 标题 + 季集信息
- ⭐ 评分 / 媒体类型 / TMDB & IMDB ID
- 📝 简介
- 🔗 TMDB / 豆瓣 / IMDb 快捷链接
- 🖼 封面图片（可选）
