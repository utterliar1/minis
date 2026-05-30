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

# ==================== 配置（从 diybotset.json 读取） ====================
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

logger.info(f"[emby-notify] 插件已加载, MONITOR_CHATS={MONITOR_CHATS}, PUSHPLUS_TOKEN={'已设置' if PUSHPLUS_TOKEN else '未设置'}")


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
        logger.warning("[emby-notify] pushplus_token 未配置")
        return
    payload = {"token": PUSHPLUS_TOKEN, "title": title, "content": html, "template": "html"}
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

    push_title = f"📺 新入库：{info['title']}"
    if info.get("season") or info.get("episode"):
        push_title += f" {info.get('season', '')} {info.get('episode', '')}"

    logger.info(f"[emby-notify] 检测到新入库: {push_title}")
    html = build_html(info)
    await push_to_wechat(push_title, html)
