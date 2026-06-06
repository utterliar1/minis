# 🤖 Telegram Codex Bot

一个基于 OpenAI API 的 Telegram AI 助手机器人，支持文字对话、图片识别、用户管理、消息转发等功能。

## ✨ 功能特性

- 💬 **AI 对话** — 支持多轮对话，保留上下文历史
- 🖼️ **图片识别** — 发送图片可使用 Vision 模型分析
- 🔄 **模型切换** — 支持 GPT-4o / GPT-4o Mini / GPT-3.5 Turbo 切换
- 📨 **消息转发** — 用户消息自动转发给管理员，管理员可直接回复
- 👤 **用户管理** — 封禁/解封用户，支持回复消息直接封禁
- 📊 **用户列表** — 查看所有使用过机器人的用户
- 📢 **群发消息** — 向所有用户发送广播消息
- 🛡️ **速率限制** — 防止单个用户过度使用
- 🎯 **命令菜单** — Telegram 内置命令快捷输入

## 📦 部署方式

### Docker 部署（推荐）

```bash
# 1. 克隆仓库
git clone https://github.com/utterliar1/minis.git
cd minis/telegram-bot

# 2. 配置环境变量
cp .env.example .env
# 编辑 .env 填入你的配置

# 3. Docker 构建运行
docker build -t telegram-bot .
docker run -d --name bot --restart unless-stopped \
  -v $(pwd)/data:/app/data \
  --env-file .env \
  telegram-bot
```

### 直接运行

```bash
pip install -r requirements.txt
cp .env.example .env
# 编辑 .env 填入你的配置
python telegram_codex_bot.py
```

## ⚙️ 环境变量

| 变量名 | 说明 | 必填 |
|--------|------|------|
| `TELEGRAM_TOKEN` | Telegram Bot Token（从 @BotFather 获取） | ✅ |
| `OWNER_ID` | 管理员的 Telegram 用户 ID | ✅ |
| `OPENAI_API_KEY` | OpenAI API Key | ✅ |
| `BASE_URL` | API 基础地址（默认 `https://api.openai.com/v1`） | ❌ |
| `CHAT_MODEL` | 对话模型（默认 `gpt-3.5-turbo`） | ❌ |
| `VISION_MODEL` | 图片识别模型（默认 `gpt-4o-mini`） | ❌ |
| `SYSTEM_PROMPT` | 系统提示词 | ❌ |
| `MAX_HISTORY` | 每用户保留对话轮数（默认 20） | ❌ |
| `RATE_LIMIT_MSGS` | 每分钟最大消息数（默认 10） | ❌ |

## 📋 命令列表

### 所有用户
| 命令 | 说明 |
|------|------|
| `/start` | 启动机器人 |
| `/help` | 帮助信息 |
| `/clear` | 清空对话历史 |

### 管理员专属
| 命令 | 说明 |
|------|------|
| `/model` | 切换 AI 模型 |
| `/status` | 查看机器人状态 |
| `/users` | 查看用户列表 |
| `/block` | 封禁用户（支持回复消息直接封禁） |
| `/unblock` | 解封用户（支持回复消息直接解封） |
| `/broadcast` | 群发消息 |
| `/setprompt` | 修改系统提示词 |
| `/testmsg` | 发送测试消息 |

## 🗂️ 项目结构

```
telegram-bot/
├── telegram_codex_bot.py   # 主程序
├── requirements.txt        # Python 依赖
├── Dockerfile              # Docker 构建文件
├── .env.example            # 环境变量示例
├── .gitignore
├── run_bot.sh              # 快速启动脚本
├── start_bot.sh            # 启动脚本（带日志）
├── stop_bot.sh             # 停止脚本
├── status_bot.sh           # 状态检查脚本
└── README.md
```

## 📄 License

MIT
