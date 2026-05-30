#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
emby-notify - 新入库消息监控 & PushPlus 微信推送
独立运行，不依赖 jbot，用自己的 Telethon 客户端监听指定 bot 的消息。

环境变量：
  API_ID          - Telegram API ID (https://my.telegram.org)
  API_HASH        - Telegram API Hash
  MONITOR_CHATS   - 监听的 bot 用户名或 chat ID，多个用逗号分隔
  PUSHPLUS_TOKEN  - PushPlus 推送 token (https://www.pushplus.plus)
  PUSHPLUS_TOPIC  - PushPlus 群组编码（可选）
"""

import asyncio
import logging
import os
import re

import httpx
from telethon import TelegramClient, events

# ==================== 日志 ====================
logging.basicConfig(
    format="%(asctime)s [%(levelname)s] %(message)s",
    level=logging.INFO,
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger("emby-notify")

# ==================== 配置 ====================
API_ID = int(os.getenv("API_ID", "0"))
API_HASH = os.getenv("API_HASH", "")
SESSION = os.getenv("SESSION_NAME", "emby_notify")
PUSHPLUS_TOKEN = os.getenv("PUSHPLUS_TOKEN", "")
PUSHPLUS_TOPIC = os.getenv("PUSHPLUS_TOPIC", "")
PUSHPLUS_URL = "http://www.pushplus.plus/send"

_raw = os.getenv("MONITOR_CHATS", "")
MONITOR_CHATS = []
for item in _raw.split(","):
    item = item.strip()
    if not item:
        continue
    try:
        MONITOR_CHATS.append(int(item))
    except ValueError:
        MONITOR_CHATS.append(item)


# ==================== 消息解析 ====================
def parse_media(text):
    """解析新入库消息，返回 dict 或 None"""
    if not text or "新入库" not in text:
        return None

    info = {}
    m = re.search(r"[📺🎬]\s*新入库\s+(\S+)\s+(.+?)(?:\s+[Ss]\d+|\s*$)", text)
    if m:
        info["type"] = m.group(1)
        info["title"] = m.group(2).strip()
    if not info.get("title"):
        return None

    m = re.search(r"([Ss]\d+)\s+([Ee][\d\-]+)", text)
    if m:
        info["season"] = m.group(1).upper()
        info["episode"] = m.group(2).upper()

    m = re.search(r"⭐️?\s*评分[：:]\s*(\S+)", text)
    if m:
        info["rating"] = m.group(1)

    m = re.search(r"[📺🎬]\s*媒体类型[：:]\s*(\S+)", text)
    if m:
        info["media_type"] = m.group(1)

    m = re.search(r"TMDB\s*ID[：:]\s*(\d+)", text)
    if m:
        info["tmdb_id"] = m.group(1)

    m = re.search(r"IMDB\s*ID[：:]\s*(tt\d+)", text)
    if m:
        info["imdb_id"] = m.group(1)

    m = re.search(r"🕒\s*操作时间[：:]\s*(.+)", text)
    if m:
        info["time"] = m.group(1).strip()

    m = re.search(r"📝\s*简介[：:]\s*(.+?)(?:\n\n|\Z)", text, re.DOTALL)
    if m:
        info["synopsis"] = m.group(1).strip()

    m = re.search(r"TMDB\s*\((https?://[^)]+)\)", text)
    if m:
        info["tmdb_link"] = m.group(1)
    m = re.search(r"豆瓣\s*\((https?://[^)]+)\)", text)
    if m:
        info["douban_link"] = m.group(1)
    m = re.search(r"IMDb\s*\((https?://[^)]+)\)", text)
    if m:
        info["imdb_link"] = m.group(1)

    m = re.search(r"🍺\s*(.+)", text)
    if m:
        info["comment"] = m.group(1).strip()

    return info


# ==================== 构造 HTML ====================
def build_html(info):
    title = info.get("title", "未知")
    se = f" {info.get('season', '')} {info.get('episode', '')}" if info.get("season") or info.get("episode") else ""

    rows = []
    if info.get("media_type"):
        rows.append(("媒体类型", info["media_type"]))
    if info.get("rating"):
        rows.append(("⭐ 评分", info["rating"]))
    if info.get("tmdb_id"):
        rows.append(("TMDB ID", info["tmdb_id"]))
    if info.get("imdb_id"):
        rows.append(("IMDB ID", info["imdb_id"]))
    if info.get("time"):
        rows.append(("操作时间", info["time"]))

    tr = ""
    for label, value in rows:
        tr += f'<tr><td style="padding:8px 12px;color:#666;white-space:nowrap">{label}</td><td style="padding:8px 12px;color:#333">{value}</td></tr>'

    html = f'''<div style="font-family:-apple-system,sans-serif;max-width:600px;margin:0 auto;padding:20px">
<h2 style="color:#333;border-bottom:2px solid #4CAF50;padding-bottom:12px">📺 {title}{se}</h2>
<table style="width:100%;border-collapse:collapse;margin:14px 0">{tr}</table>'''

    if info.get("synopsis"):
        html += f'<div style="background:#f8f9fa;padding:14px 18px;border-radius:8px;margin:16px 0"><strong>📝 简介</strong><br><span style="color:#555;line-height:1.6">{info["synopsis"]}</span></div>'

    if info.get("comment"):
        html += f'<div style="background:#fff8e1;padding:10px 18px;border-radius:8px;margin:16px 0">🍺 {info["comment"]}</div>'

    links = []
    if info.get("tmdb_link"):
        links.append(f'<a href="{info["tmdb_link"]}" style="color:#1976D2;text-decoration:none">🔗 TMDB</a>')
    if info.get("douban_link"):
        links.append(f'<a href="{info["douban_link"]}" style="color:#2E7D32;text-decoration:none">✳️ 豆瓣</a>')
    if info.get("imdb_link"):
        links.append(f'<a href="{info["imdb_link"]}" style="color:#F57C00;text-decoration:none">🌟 IMDb</a>')
    if links:
        html += f'<div style="margin-top:16px;padding-top:16px;border-top:1px solid #eee">{" &nbsp;|&nbsp; ".join(links)}</div>'

    html += "</div>"
    return html


# ==================== PushPlus 推送 ====================
async def push_to_wechat(title, html):
    if not PUSHPLUS_TOKEN:
        logger.warning("PUSHPLUS_TOKEN 未配置，跳过推送")
        return
    payload = {
        "token": PUSHPLUS_TOKEN,
        "title": title,
        "content": html,
        "template": "html",
    }
    if PUSHPLUS_TOPIC:
        payload["topic"] = PUSHPLUS_TOPIC
    try:
        async with httpx.AsyncClient(timeout=15) as session:
            resp = await session.post(PUSHPLUS_URL, json=payload)
            result = resp.json()
            if result.get("code") == 200:
                logger.info("推送成功: %s", title)
            else:
                logger.error("推送失败: %s", result)
    except Exception as e:
        logger.error("请求异常: %s", e)


# ==================== 主程序 ====================
async def main():
    if not API_ID or not API_HASH:
        logger.error("请设置 API_ID 和 API_HASH 环境变量")
        return
    if not MONITOR_CHATS:
        logger.error("请设置 MONITOR_CHATS 环境变量（bot 用户名或 chat ID）")
        return

    client = TelegramClient(SESSION, API_ID, API_HASH)

    @client.on(events.NewMessage(chats=MONITOR_CHATS))
    @client.on(events.MessageEdited(chats=MONITOR_CHATS))
    async def on_new_media(event):
        text = event.message.text or event.message.message or ""
        info = parse_media(text)
        if not info:
            return

        push_title = f"📺 新入库：{info['title']}"
        if info.get("season") or info.get("episode"):
            push_title += f" {info.get('season', '')} {info.get('episode', '')}"

        logger.info("检测到新入库: %s", push_title)
        html = build_html(info)
        await push_to_wechat(push_title, html)

    await client.start()
    logger.info("emby-notify 已启动，监听: %s", MONITOR_CHATS)
    await client.run_until_disconnected()


if __name__ == "__main__":
    asyncio.run(main())
