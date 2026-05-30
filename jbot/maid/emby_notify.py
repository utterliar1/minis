#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
emby-notify - 新入库消息监控 & PushPlus 微信推送

在 diybotset.json 中添加以下配置：
  "monitor_chats": "@your_bot_username",
  "pushplus_token": "你的token",
  "pushplus_topic": ""
"""

import re

import httpx
from telethon import events

from jbot import client, logger, diybotset

# ==================== 配置 ====================
MONITOR_CHATS = []
_raw = str(diybotset.get("monitor_chats", ""))
for item in _raw.split(","):
    item = item.strip()
    if not item:
        continue
    try:
        MONITOR_CHATS.append(int(item))
    except ValueError:
        MONITOR_CHATS.append(item)

PUSHPLUS_TOKEN = diybotset.get("pushplus_token", "")
PUSHPLUS_TOPIC = diybotset.get("pushplus_topic", "")
PUSHPLUS_URL = "https://www.pushplus.plus/send"

logger.info(f"[emby-notify] 已加载, MONITOR_CHATS={MONITOR_CHATS}, TOKEN={'OK' if PUSHPLUS_TOKEN else '未设置'}")


# ==================== 消息解析 ====================
def parse_media(text):
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

    m = re.search(r"[⭐️]?\s*评分[：:]\s*(\S+)", text)
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

    m = re.search(r"[🕒]\s*操作时间[：:]\s*(.+)", text)
    if m:
        info["time"] = m.group(1).strip()

    m = re.search(r"[📝]\s*简介[：:]\s*(.+?)(?:\n\n|\\Z)", text, re.DOTALL)
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

    m = re.search(r"[🍺]\s*(.+)", text)
    if m:
        info["comment"] = m.group(1).strip()

    return info


# ==================== 构造 Markdown ====================
def build_markdown(info):
    title = info.get("title", "未知")
    media_type = info.get("type", "")
    se = ""
    if info.get("season") or info.get("episode"):
        se = f" {info.get('season', '')} {info.get('episode', '')}"

    lines = [f"**📺 新入库 {media_type} {title}{se}**", ""]

    if info.get("media_type"):
        lines.append(f"- **媒体类型：** {info['media_type']}")
    if info.get("rating"):
        lines.append(f"- **⭐ 评分：** {info['rating']}")
    if info.get("tmdb_id"):
        lines.append(f"- **TMDB ID：** {info['tmdb_id']}")
    if info.get("imdb_id"):
        lines.append(f"- **IMDB ID：** {info['imdb_id']}")
    if info.get("time"):
        lines.append(f"- **操作时间：** {info['time']}")

    if info.get("synopsis"):
        lines.append("")
        lines.append(f"> {info['synopsis']}")

    if info.get("comment"):
        lines.append("")
        lines.append(f"🍺 {info['comment']}")

    links = []
    if info.get("tmdb_link"):
        links.append(f"[TMDB]({info['tmdb_link']})")
    if info.get("douban_link"):
        links.append(f"[豆瓣]({info['douban_link']})")
    if info.get("imdb_link"):
        links.append(f"[IMDb]({info['imdb_link']})")
    if links:
        lines.append("")
        lines.append(" | ".join(links))

    return "\n".join(lines)


# ==================== PushPlus 推送 ====================
async def push_to_wechat(title, content):
    if not PUSHPLUS_TOKEN:
        logger.warning("[emby-notify] pushplus_token 未配置")
        return
    payload = {
        "token": PUSHPLUS_TOKEN,
        "title": title,
        "content": content,
        "template": "markdown",
        "channel": "wechat",
    }
    if PUSHPLUS_TOPIC:
        payload["topic"] = PUSHPLUS_TOPIC
    try:
        async with httpx.AsyncClient(timeout=15) as session:
            resp = await session.post(PUSHPLUS_URL, json=payload)
            result = resp.json()
            if result.get("code") == 200:
                logger.info(f"[emby-notify] 推送成功: {title}")
            else:
                logger.error(f"[emby-notify] 推送失败: {result}")
    except Exception as e:
        logger.error(f"[emby-notify] 请求异常: {e}")


# ==================== 监听入口 ====================
@client.on(events.NewMessage(chats=MONITOR_CHATS))
@client.on(events.MessageEdited(chats=MONITOR_CHATS))
async def on_new_media(event):
    text = event.message.text or event.message.message or ""
    logger.info(f"[emby-notify] 收到消息: {text[:80]}...")
    info = parse_media(text)
    if not info:
        return

    logger.info(f"[emby-notify] 检测到新入库: {info['title']}")
    md = build_markdown(info)
    await push_to_wechat("入库通知", md)
