import os
import html
import openai
import logging
import re
import base64
import asyncio
import time
import sqlite3
import signal
import sys
from datetime import datetime
from collections import defaultdict
from telegram import Update, InlineKeyboardButton, InlineKeyboardMarkup, BotCommand
from telegram.ext import (
    ApplicationBuilder, CommandHandler, MessageHandler,
    CallbackQueryHandler, filters, ContextTypes
)
from telegram.constants import ParseMode

# ======================== 配置 ========================

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY", "")
TELEGRAM_TOKEN = os.getenv("TELEGRAM_TOKEN", "your_telegram_token")
OWNER_ID = os.getenv("OWNER_ID", "your_owner_id")
BASE_URL = os.getenv("BASE_URL", "https://api.openai.com/v1")

CHAT_MODEL = os.getenv("CHAT_MODEL", "gpt-3.5-turbo")
VISION_MODEL = os.getenv("VISION_MODEL", "gpt-4o-mini")

MAX_IMAGE_SIZE = int(os.getenv("MAX_IMAGE_SIZE", 5 * 1024 * 1024))
MAX_MSG_LENGTH = 4000
MAX_HISTORY = int(os.getenv("MAX_HISTORY", "20"))  # 每用户保留对话轮数
RATE_LIMIT_MSGS = int(os.getenv("RATE_LIMIT_MSGS", "10"))  # 每分钟最大消息数
RATE_LIMIT_WINDOW = 60
SYSTEM_PROMPT = os.getenv("SYSTEM_PROMPT", "你是一个有帮助的 AI 助手，用中文回复。")

TEST_USER_ID = 12345678
TEST_USERNAME = "TestUser"

# 可用模型列表（可通过 /model 切换）
MODELS = {
    "1": ("gpt-4o", "GPT-4o"),
    "2": ("gpt-4o-mini", "GPT-4o Mini"),
    "3": ("gpt-3.5-turbo", "GPT-3.5 Turbo"),
}

# 校验
if not TELEGRAM_TOKEN or TELEGRAM_TOKEN == "your_telegram_token":
    raise ValueError("请设置 TELEGRAM_TOKEN")
if not OWNER_ID or OWNER_ID == "your_owner_id":
    raise ValueError("请设置 OWNER_ID")
if not OPENAI_API_KEY:
    raise ValueError("请设置 OPENAI_API_KEY")

OWNER_ID = int(OWNER_ID)
START_TIME = time.time()

# ======================== 日志 ========================

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[logging.StreamHandler()]
)
logger = logging.getLogger("bot")

# ======================== 数据库 ========================

DB_PATH = os.getenv("DB_PATH", "/app/data/bot.db")

def init_db():
    os.makedirs(os.path.dirname(DB_PATH), exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("""CREATE TABLE IF NOT EXISTS users (
        user_id INTEGER PRIMARY KEY,
        username TEXT,
        first_name TEXT,
        first_seen TEXT,
        last_seen TEXT,
        msg_count INTEGER DEFAULT 0,
        blocked INTEGER DEFAULT 0
    )""")
    c.execute("""CREATE TABLE IF NOT EXISTS messages (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER,
        role TEXT,
        content TEXT,
        timestamp TEXT
    )""")
    conn.commit()
    conn.close()
    logger.info("数据库初始化完成: %s" % DB_PATH)

def db_execute(query, params=(), fetch=False):
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute(query, params)
    result = c.fetchall() if fetch else None
    conn.commit()
    conn.close()
    return result

def log_user(user):
    now = datetime.now().isoformat()
    db_execute(
        """INSERT INTO users (user_id, username, first_name, first_seen, last_seen, msg_count)
           VALUES (?, ?, ?, ?, ?, 1)
           ON CONFLICT(user_id) DO UPDATE SET
           username=?, first_name=?, last_seen=?, msg_count=msg_count+1""",
        (user.id, user.username, user.first_name, now, now,
         user.username, user.first_name, now)
    )

def is_blocked(user_id):
    result = db_execute("SELECT blocked FROM users WHERE user_id=?", (user_id,), fetch=True)
    return result and result[0][0] == 1

# ======================== 对话历史 ========================

# 内存缓存：{user_id: [{"role": "user/assistant", "content": "..."}]}
chat_history = defaultdict(list)

def add_history(user_id, role, content):
    chat_history[user_id].append({"role": role, "content": content})
    if len(chat_history[user_id]) > MAX_HISTORY * 2:
        chat_history[user_id] = chat_history[user_id][-(MAX_HISTORY * 2):]
    # 持久化
    db_execute(
        "INSERT INTO messages (user_id, role, content, timestamp) VALUES (?, ?, ?, ?)",
        (user_id, role, str(content)[:2000], datetime.now().isoformat())
    )

def get_history(user_id):
    return list(chat_history[user_id])

def clear_history(user_id):
    chat_history[user_id] = []
    db_execute("DELETE FROM messages WHERE user_id=?", (user_id,))
    return True

# ======================== 速率限制 ========================

rate_limit_cache = defaultdict(list)  # {user_id: [timestamp, ...]}

def check_rate_limit(user_id):
    now = time.time()
    rate_limit_cache[user_id] = [t for t in rate_limit_cache[user_id] if now - t < RATE_LIMIT_WINDOW]
    if len(rate_limit_cache[user_id]) >= RATE_LIMIT_MSGS:
        return False
    rate_limit_cache[user_id].append(now)
    return True

# ======================== 辅助函数 ========================

def esc(text):
    """HTML 转义"""
    return html.escape(str(text), quote=True)

def uptime_str():
    delta = int(time.time() - START_TIME)
    h, m, s = delta // 3600, (delta % 3600) // 60, delta % 60
    return "%dh %dm %ds" % (h, m, s)

async def send_html(context, chat_id, text, **kwargs):
    """安全发送 HTML 消息"""
    if len(text) <= MAX_MSG_LENGTH:
        try:
            await context.bot.send_message(chat_id=chat_id, text=text, parse_mode=ParseMode.HTML, **kwargs)
        except Exception:
            await context.bot.send_message(chat_id=chat_id, text=esc(text), **kwargs)
    else:
        for i in range(0, len(text), MAX_MSG_LENGTH):
            chunk = text[i:i + MAX_MSG_LENGTH]
            try:
                await context.bot.send_message(chat_id=chat_id, text=chunk, parse_mode=ParseMode.HTML, **kwargs)
            except Exception:
                await context.bot.send_message(chat_id=chat_id, text=esc(chunk), **kwargs)

async def send_photo_html(context, chat_id, photo, caption, **kwargs):
    try:
        await context.bot.send_photo(chat_id=chat_id, photo=photo, caption=caption, parse_mode=ParseMode.HTML, **kwargs)
    except Exception:
        await context.bot.send_photo(chat_id=chat_id, photo=photo, caption=esc(caption), **kwargs)

async def download_photo_b64(update, context):
    photo = max(update.message.photo, key=lambda x: x.file_size)
    if photo.file_size > MAX_IMAGE_SIZE:
        raise Exception("图片最大 %.1fMB" % (MAX_IMAGE_SIZE / 1048576))
    file = await context.bot.get_file(photo.file_id)
    data = await file.download_as_bytearray()
    return base64.b64encode(data).decode()

def extract_target_user_id(message):
    text = message.text or message.caption or ""
    # 支持多种 ID 格式：🆔 ID：xxx、用户 ID：xxx、ID：xxx
    m = re.search(r"(?:🆔\s*)?(?:用户\s*)?ID[：:]\s*(\d+)", text)
    return int(m.group(1)) if m else None

def make_user_link(user):
    name = esc(user.first_name or user.username or str(user.id))
    if user.username:
        return '<a href="https://t.me/%s">%s</a>' % (user.username, name)
    return '<a href="tg://user?id=%d">%s</a>' % (user.id, name)

def uid_link(uid):
    return '<a href="tg://user?id=%d">%d</a>' % (uid, uid)

# ======================== OpenAI ========================

client = None

def get_client():
    global client
    if client is None:
        client = openai.OpenAI(api_key=OPENAI_API_KEY, base_url=BASE_URL)
    return client

async def call_gpt(messages, model=None, max_retries=2):
    if model is None:
        model = CHAT_MODEL
    ai = get_client()
    last_err = None
    for attempt in range(max_retries + 1):
        try:
            resp = ai.chat.completions.create(
                model=model, messages=messages, temperature=0.7, timeout=60
            )
            return resp.choices[0].message.content
        except openai.AuthenticationError:
            raise Exception("❌ API Key 无效，请检查配置")
        except openai.RateLimitError as e:
            last_err = e
            if attempt < max_retries:
                await asyncio.sleep(2 * (attempt + 1))
                continue
            raise Exception("❌ API 限流，请稍后再试")
        except openai.APIStatusError as e:
            last_err = e
            if e.status_code >= 500 and attempt < max_retries:
                await asyncio.sleep(2 * (attempt + 1))
                continue
            raise Exception("❌ API 错误 (HTTP %d)" % e.status_code)
        except Exception as e:
            last_err = e
            if attempt < max_retries:
                await asyncio.sleep(2 * (attempt + 1))
                continue
            raise Exception("❌ AI 调用失败: %s" % str(e)[:200])

async def check_api_health():
    """检测 API 健康状态"""
    ai = get_client()
    try:
        ai.chat.completions.create(
            model=CHAT_MODEL,
            messages=[{"role": "user", "content": "hi"}],
            max_tokens=5, timeout=10
        )
        return True, "正常"
    except openai.AuthenticationError:
        return False, "Key 无效"
    except openai.RateLimitError:
        return False, "限流中"
    except openai.APIStatusError as e:
        return False, "HTTP %d" % e.status_code
    except Exception as e:
        return False, str(e)[:50]

# ======================== 命令 ========================

async def cmd_start(update, context):
    await update.message.reply_text(
        "👋 你好！我是消息转发 + AI 助手。\n\n"
        "📌 使用说明：\n"
        "• 私聊我发送文字/图片，我会转发给管理员\n"
        "• 管理员可以直接和我对话（AI 模式）\n\n"
        "发送 /help 查看所有命令"
    )

async def cmd_help(update, context):
    uid = update.message.from_user.id
    is_owner = uid == OWNER_ID
    help_text = "📖 <b>命令列表</b>\n\n"
    help_text += "/start - 开始使用\n"
    help_text += "/help - 查看帮助\n"
    help_text += "/clear - 清空对话记录\n"

    if is_owner:
        help_text += "\n<b>🔧 管理员命令</b>\n"
        help_text += "/status - 系统状态\n"
        help_text += "/model - 切换 AI 模型\n"
        help_text += "/users - 用户列表\n"
        help_text += "/block &lt;用户ID&gt; - 封禁用户\n"
        help_text += "/unblock &lt;用户ID&gt; - 解封用户\n"
        help_text += "/broadcast &lt;内容&gt; - 群发消息\n"
        help_text += "/testmsg &lt;内容&gt; - 模拟用户消息\n"
        help_text += "/setprompt &lt;提示词&gt; - 设置系统提示\n"

    await send_html(context, uid, help_text)

async def cmd_clear(update, context):
    uid = update.message.from_user.id
    clear_history(uid)
    await update.message.reply_text("🗑️ 对话记录已清空")

async def cmd_status(update, context):
    if update.message.from_user.id != OWNER_ID:
        return

    # 统计
    users_count = db_execute("SELECT COUNT(*) FROM users", fetch=True)[0][0]
    total_msgs = db_execute("SELECT SUM(msg_count) FROM users", fetch=True)[0][0] or 0
    blocked_count = db_execute("SELECT COUNT(*) FROM users WHERE blocked=1", fetch=True)[0][0]
    api_ok, api_msg = await check_api_health()

    status = "📊 <b>系统状态</b>\n\n"
    status += "⏱ 运行时间: %s\n" % uptime_str()
    status += "👥 用户总数: %d\n" % users_count
    status += "💬 消息总数: %d\n" % total_msgs
    status += "🚫 已封禁: %d\n" % blocked_count
    status += "🧠 对话模型: %s\n" % CHAT_MODEL
    status += "👁 视觉模型: %s\n" % VISION_MODEL
    status += "📏 历史上限: %d 轮\n" % MAX_HISTORY
    status += "\n🔌 <b>API 状态</b>\n"
    status += "OpenAI: %s %s\n" % ("✅" if api_ok else "❌", api_msg)
    status += "Telegram: ✅ 在线\n"
    status += "\n🌐 BASE_URL: <code>%s</code>" % esc(BASE_URL)

    await send_html(context, update.message.from_user.id, status)

async def cmd_model(update, context):
    global CHAT_MODEL
    if update.message.from_user.id != OWNER_ID:
        return

    # 如果带参数则直接切换
    if context.args:
        key = context.args[0]
        if key in MODELS:
            CHAT_MODEL = MODELS[key][0]
            await update.message.reply_text("✅ 已切换到: %s" % MODELS[key][1])
            return
        else:
            await update.message.reply_text("❌ 无效选项，发送 /model 查看列表")
            return

    buttons = []
    for k in sorted(MODELS.keys()):
        label = MODELS[k][1]
        if MODELS[k][0] == CHAT_MODEL:
            label = "✅ " + label
        buttons.append([InlineKeyboardButton(label, callback_data="model_%s" % k)])
    await update.message.reply_text(
        "🧠 当前模型: %s\n\n选择要切换的模型：" % CHAT_MODEL,
        reply_markup=InlineKeyboardMarkup(buttons)
    )

async def cmd_users(update, context):
    if update.message.from_user.id != OWNER_ID:
        return
    rows = db_execute(
        "SELECT user_id, username, first_name, msg_count, blocked, last_seen FROM users ORDER BY last_seen DESC LIMIT 30",
        fetch=True
    )
    if not rows:
        await update.message.reply_text("📭 暂无用户记录")
        return
    text = "👥 <b>最近用户</b> (前30)\n\n"
    for uid, uname, fname, cnt, blk, last in rows:
        status = "🚫" if blk else "✅"
        name = esc(fname or uname or str(uid))
        text += "%s <b>%s</b> (ID: %d)\n   消息: %d | %s\n\n" % (status, name, uid, cnt, last[:16])
    await send_html(context, update.message.from_user.id, text)

async def cmd_block(update, context):
    if update.message.from_user.id != OWNER_ID:
        return
    target = None
    name = ""
    # 回复 bot 转发的消息 -> 从消息文本提取真实用户 ID
    if update.message.reply_to_message:
        target = extract_target_user_id(update.message.reply_to_message)
        if target:
            # 从 "来自用户 xxx:" 提取名字
            text = update.message.reply_to_message.text or update.message.reply_to_message.caption or ""
            m = re.search(r"(?:👤\s*)?(?:用户|来自用户)[：:]\s*(.+?)(?:\n|$|\s+<)", text)
            if not m:
                m = re.search(r"@([\w_]+)", text)
            name = m.group(1) if m else str(target)
        else:
            # fallback: 普通回复
            target = update.message.reply_to_message.from_user.id
            name = update.message.reply_to_message.from_user.first_name or str(target)
    elif context.args:
        try:
            target = int(context.args[0])
            name = str(target)
        except ValueError:
            await update.message.reply_text("❌ 用户ID 必须是数字")
            return
    if not target:
        await update.message.reply_text("用法:\n回复用户消息: /block\n或: /block <用户ID>")
        return
    db_execute("UPDATE users SET blocked=1 WHERE user_id=?", (target,))
    if target in chat_history:
        chat_history[target] = []
    await update.message.reply_text("🚫 已封禁 %s (%d)" % (name, target))

async def cmd_unblock(update, context):
    if update.message.from_user.id != OWNER_ID:
        return
    target = None
    name = ""
    if update.message.reply_to_message:
        target = extract_target_user_id(update.message.reply_to_message)
        if target:
            text = update.message.reply_to_message.text or update.message.reply_to_message.caption or ""
            m = re.search(r"(?:👤\s*)?(?:用户|来自用户)[：:]\s*(.+?)(?:\n|$|\s+<)", text)
            if not m:
                m = re.search(r"@([\w_]+)", text)
            name = m.group(1) if m else str(target)
        else:
            target = update.message.reply_to_message.from_user.id
            name = update.message.reply_to_message.from_user.first_name or str(target)
    elif context.args:
        try:
            target = int(context.args[0])
            name = str(target)
        except ValueError:
            await update.message.reply_text("❌ 用户ID 必须是数字")
            return
    if not target:
        await update.message.reply_text("用法:\n回复用户消息: /unblock\n或: /unblock <用户ID>")
        return
    db_execute("UPDATE users SET blocked=0 WHERE user_id=?", (target,))
    await update.message.reply_text("✅ 已解封 %s (%d)" % (name, target))

async def cmd_broadcast(update, context):
    if update.message.from_user.id != OWNER_ID:
        return
    content = " ".join(context.args).strip() if context.args else ""
    if not content:
        await update.message.reply_text("用法: /broadcast <内容>")
        return
    users = db_execute("SELECT user_id FROM users WHERE blocked=0", fetch=True)
    success, fail = 0, 0
    for (uid,) in users:
        try:
            await context.bot.send_message(chat_id=uid, text=content)
            success += 1
        except Exception:
            fail += 1
        await asyncio.sleep(0.05)  # 限流
    await update.message.reply_text("📢 群发完成: 成功 %d, 失败 %d" % (success, fail))

async def cmd_setprompt(update, context):
    global SYSTEM_PROMPT
    if update.message.from_user.id != OWNER_ID:
        return
    prompt = " ".join(context.args).strip() if context.args else ""
    if not prompt:
        await update.message.reply_text(
            "当前系统提示:\n%s\n\n用法: /setprompt <提示词>" % SYSTEM_PROMPT
        )
        return
    SYSTEM_PROMPT = prompt
    await update.message.reply_text("✅ 系统提示已更新")

async def cmd_testmsg(update, context):
    if update.message.from_user.id != OWNER_ID:
        await update.message.reply_text("❌ 仅管理员可用")
        return
    has_photo = bool(update.message.photo)
    if update.message.caption:
        parts = update.message.caption.split(" ", 1)
        test_content = parts[1].strip() if len(parts) > 1 else ""
    else:
        test_content = " ".join(context.args).strip() if context.args else ""
    if not test_content and not has_photo:
        await update.message.reply_text("📝 用法: /testmsg 内容\n或发图 + caption /testmsg 描述")
        return
    user_link = '<a href="https://t.me/%s">%s</a>' % (TEST_USERNAME, TEST_USERNAME)
    caption = "来自测试用户 %s 的消息:\n\n%s\n\n用户 ID: %s" % (user_link, esc(test_content), uid_link(TEST_USER_ID))
    if has_photo:
        await send_photo_html(context, OWNER_ID, update.message.photo[-1].file_id, caption)
    else:
        await send_html(context, OWNER_ID, caption)
    await update.message.reply_text("✅ 测试消息已发送")

# ======================== 回调按钮 ========================

async def callback_model(update, context):
    global CHAT_MODEL
    query = update.callback_query
    await query.answer()
    key = query.data.replace("model_", "")
    if key in MODELS:
        CHAT_MODEL = MODELS[key][0]
        await query.edit_message_text("✅ 已切换到: %s" % MODELS[key][1])

# ======================== 主消息处理 ========================

async def handle_message(update, context):
    try:
        user = update.message.from_user
        uid = user.id
        is_owner = uid == OWNER_ID
        has_photo = bool(update.message.photo)
        msg_text = update.message.caption if has_photo else (update.message.text or "")

        # 记录用户
        log_user(user)

        # 封禁检查
        if not is_owner and is_blocked(uid):
            await update.message.reply_text("🚫 <b>你已被封禁</b>\n无法使用此机器人，请联系管理员。", parse_mode=ParseMode.HTML)
            return

        # 速率限制（非管理员）
        if not is_owner and not check_rate_limit(uid):
            await update.message.reply_text("⚠️ 消息过于频繁，请稍后再试")
            return

        user_link = make_user_link(user)

        # ---- 管理员回复用户 ----
        if is_owner and update.message.reply_to_message and update.message.reply_to_message.from_user.id == context.bot.id:
            target_id = extract_target_user_id(update.message.reply_to_message)
            if not target_id:
                await update.message.reply_text("❌ 无法识别目标用户")
                return
            if target_id == TEST_USER_ID:
                reply = "✅ 测试回复成功！\n内容：%s%s" % (esc(msg_text), "\n（带图）" if has_photo else "")
                await update.message.reply_text(reply)
                return
            reply_content = "━━━━ 📩 <b>管理员回复</b> ━━━━\n\n%s\n\n━━━━━━━━━━━━━━━" % esc(msg_text)
            if has_photo:
                await send_photo_html(context, target_id, update.message.photo[-1].file_id, reply_content)
            else:
                await send_html(context, target_id, reply_content)
            await update.message.reply_text("✅ 已回复用户")
            return

        # ---- 用户消息转发 ----
        if update.message.chat.type == "private" and not is_owner:
            # 美化的转发卡片
            time_str = datetime.now().strftime("%H:%M")
            caption_parts = [
                "━━━━ 📨 <b>新消息</b> ━━━━",
                "",
                "👤 <b>用户：</b>%s" % user_link,
                "🆔 <b>ID：</b>%s" % uid_link(uid),
            ]
            if user.username:
                caption_parts.append("📛 <b>@%s</b>" % esc(user.username))
            caption_parts.append("🕐 <b>时间：</b>%s" % time_str)
            caption_parts.append("")
            caption_parts.append("💬 <b>消息：</b>")
            caption_parts.append(esc(msg_text) if msg_text else "（无文字）")
            caption_parts.append("")
            caption_parts.append("━━━━━━━━━━━━━━━")
            caption = "\n".join(caption_parts)
            if has_photo:
                await send_photo_html(context, OWNER_ID, update.message.photo[-1].file_id, caption)
            else:
                await send_html(context, OWNER_ID, caption)
            await update.message.reply_text("✅ 消息已发送给管理员")
            return

        # ---- 管理员 AI 对话 ----
        if is_owner and not update.message.reply_to_message:
            # 构建内容
            if has_photo and msg_text:
                content = [
                    {"type": "text", "text": msg_text},
                    {"type": "image_url", "image_url": {"url": "data:image/jpeg;base64,%s" % await download_photo_b64(update, context)}}
                ]
            elif has_photo:
                content = [
                    {"type": "image_url", "image_url": {"url": "data:image/jpeg;base64,%s" % await download_photo_b64(update, context)}}
                ]
            else:
                content = msg_text

            # 构建带历史的消息
            add_history(uid, "user", content)
            messages = [{"role": "system", "content": SYSTEM_PROMPT}]
            messages.extend(get_history(uid))

            model = VISION_MODEL if has_photo else CHAT_MODEL
            await context.bot.send_chat_action(chat_id=uid, action="typing")

            reply = await call_gpt(messages, model)
            add_history(uid, "assistant", reply)
            await send_html(context, uid, esc(reply))
            return

    except Exception as e:
        logger.error("错误: %s" % e, exc_info=True)
        try:
            await update.message.reply_text("❌ %s" % esc(str(e)[:500]))
        except Exception:
            await update.message.reply_text("❌ 出错了，请稍后再试")

async def handle_unsupported(update, context):
    await update.message.reply_text("❌ 仅支持文字和图片")

# ======================== 启动 ========================

def main():
    init_db()
    app = ApplicationBuilder().token(TELEGRAM_TOKEN).build()

    # 命令
    app.add_handler(CommandHandler("start", cmd_start))
    app.add_handler(CommandHandler("help", cmd_help))
    app.add_handler(CommandHandler("clear", cmd_clear))
    app.add_handler(CommandHandler("status", cmd_status))
    app.add_handler(CommandHandler("model", cmd_model))
    app.add_handler(CommandHandler("users", cmd_users))
    app.add_handler(CommandHandler("block", cmd_block))
    app.add_handler(CommandHandler("unblock", cmd_unblock))
    app.add_handler(CommandHandler("broadcast", cmd_broadcast))
    app.add_handler(CommandHandler("setprompt", cmd_setprompt))
    app.add_handler(CommandHandler("testmsg", cmd_testmsg))

    # 按钮回调
    app.add_handler(CallbackQueryHandler(callback_model, pattern="^model_"))

    # 消息
    app.add_handler(MessageHandler(filters.TEXT | filters.PHOTO & ~filters.COMMAND, handle_message))
    app.add_handler(MessageHandler(filters.ALL & ~filters.TEXT & ~filters.PHOTO, handle_unsupported))

    logger.info("Bot 启动 | Owner: %d | Model: %s | Vision: %s" % (OWNER_ID, CHAT_MODEL, VISION_MODEL))
    # 设置命令菜单（快速输入）
    async def post_init(application):
        commands = [
            BotCommand("start", "启动机器人"),
            BotCommand("help", "帮助信息"),
            BotCommand("clear", "清空对话历史"),
            BotCommand("model", "切换AI模型 (管理员)"),
            BotCommand("status", "查看状态 (管理员)"),
            BotCommand("users", "用户列表 (管理员)"),
            BotCommand("block", "封禁用户 (管理员, 可回复)"),
            BotCommand("unblock", "解封用户 (管理员, 可回复)"),
            BotCommand("broadcast", "群发消息 (管理员)"),
            BotCommand("setprompt", "设置提示词 (管理员)"),
        ]
        await application.bot.set_my_commands(commands)

    app.post_init = post_init
    app.run_polling(drop_pending_updates=True)

if __name__ == "__main__":
    main()
