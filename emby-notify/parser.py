"""Telegram 新入库消息解析器."""

import re
from typing import Optional


def parse_media_message(text: str) -> Optional[dict]:
    """解析新入库消息，返回结构化数据，非新入库消息返回 None."""
    if not text or "新入库" not in text:
        return None

    info: dict = {}

    # 标题行: 📺 新入库 剧集 樊笼 S01 E01-E23
    title_match = re.search(
        r"[📺🎬]\s*新入库\s+(\S+)\s+(.+?)(?:\s+[Ss]\d+|\s*$)", text
    )
    if title_match:
        info["type"] = title_match.group(1)
        info["title"] = title_match.group(2).strip()

    if not info.get("title"):
        return None

    # 季集
    se_match = re.search(r"([Ss]\d+)\s+([Ee][\d\-]+)", text)
    if se_match:
        info["season"] = se_match.group(1).upper()
        info["episode"] = se_match.group(2).upper()

    # 评分
    m = re.search(r"⭐️?\s*评分[：:]\s*(\S+)", text)
    if m:
        info["rating"] = m.group(1)

    # 媒体类型
    m = re.search(r"[📺🎬]\s*媒体类型[：:]\s*(\S+)", text)
    if m:
        info["media_type"] = m.group(1)

    # TMDB ID
    m = re.search(r"TMDB\s*ID[：:]\s*(\d+)", text)
    if m:
        info["tmdb_id"] = m.group(1)

    # IMDB ID
    m = re.search(r"IMDB\s*ID[：:]\s*(tt\d+)", text)
    if m:
        info["imdb_id"] = m.group(1)

    # 操作时间
    m = re.search(r"🕒\s*操作时间[：:]\s*(.+)", text)
    if m:
        info["time"] = m.group(1).strip()

    # 简介
    m = re.search(r"📝\s*简介[：:]\s*(.+?)(?:\n\n|\Z)", text, re.DOTALL)
    if m:
        info["synopsis"] = m.group(1).strip()

    # 链接
    m = re.search(r"TMDB\s*\((https?://[^)]+)\)", text)
    if m:
        info["tmdb_link"] = m.group(1)

    m = re.search(r"豆瓣\s*\((https?://[^)]+)\)", text)
    if m:
        info["douban_link"] = m.group(1)

    m = re.search(r"IMDb\s*\((https?://[^)]+)\)", text)
    if m:
        info["imdb_link"] = m.group(1)

    # 评论
    m = re.search(r"🍺\s*(.+)", text)
    if m:
        info["comment"] = m.group(1).strip()

    return info


def build_title(info: dict) -> str:
    """构造推送标题."""
    title = f"📺 新入库：{info['title']}"
    if info.get("season") or info.get("episode"):
        title += f" {info.get('season', '')} {info.get('episode', '')}"
    return title
