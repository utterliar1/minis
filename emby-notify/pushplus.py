"""PushPlus 微信推送模块."""

import logging

import requests

logger = logging.getLogger(__name__)

PUSHPLUS_URL = "http://www.pushplus.plus/send"


def build_html(info: dict) -> str:
    """将解析后的媒体信息转为 PushPlus HTML 模板."""
    title = info.get("title", "未知")
    media_type = info.get("media_type", info.get("type", ""))
    se = ""
    if info.get("season") or info.get("episode"):
        se = f" {info.get('season', '')} {info.get('episode', '')}"

    rows = []
    if media_type:
        rows.append(("媒体类型", media_type))
    if info.get("rating"):
        rows.append(("⭐ 评分", info["rating"]))
    if info.get("tmdb_id"):
        rows.append(("TMDB ID", info["tmdb_id"]))
    if info.get("imdb_id"):
        rows.append(("IMDB ID", info["imdb_id"]))
    if info.get("time"):
        rows.append(("操作时间", info["time"]))

    table_rows = ""
    for label, value in rows:
        table_rows += f"""
        <tr>
            <td style="padding:8px 12px;color:#666;white-space:nowrap;">{label}</td>
            <td style="padding:8px 12px;color:#333;">{value}</td>
        </tr>"""

    synopsis_html = ""
    if info.get("synopsis"):
        synopsis_html = f"""
        <div style="background:#f8f9fa;padding:14px 18px;border-radius:8px;margin:16px 0;">
            <strong>📝 简介</strong><br>
            <span style="color:#555;line-height:1.6;">{info['synopsis']}</span>
        </div>"""

    comment_html = ""
    if info.get("comment"):
        comment_html = f"""
        <div style="background:#fff8e1;padding:10px 18px;border-radius:8px;margin:16px 0;">
            🍺 {info['comment']}
        </div>"""

    links = []
    if info.get("tmdb_link"):
        links.append(f'<a href="{info["tmdb_link"]}" style="color:#1976D2;text-decoration:none;">🔗 TMDB</a>')
    if info.get("douban_link"):
        links.append(f'<a href="{info["douban_link"]}" style="color:#2E7D32;text-decoration:none;">✳️ 豆瓣</a>')
    if info.get("imdb_link"):
        links.append(f'<a href="{info["imdb_link"]}" style="color:#F57C00;text-decoration:none;">🌟 IMDb</a>')

    links_html = ""
    if links:
        links_html = f"""
        <div style="margin-top:16px;padding-top:16px;border-top:1px solid #eee;">
            {" &nbsp;|&nbsp; ".join(links)}
        </div>"""

    return f"""
    <div style="font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;max-width:600px;margin:0 auto;padding:20px;">
        <h2 style="color:#333;border-bottom:2px solid #4CAF50;padding-bottom:12px;">
            📺 {title}{se}
        </h2>
        <table style="width:100%;border-collapse:collapse;margin:14px 0;">
            {table_rows}
        </table>
        {synopsis_html}
        {comment_html}
        {links_html}
    </div>"""


def send(token: str, title: str, content: str, topic: str = "") -> dict:
    """通过 PushPlus 推送消息到微信.

    Args:
        token: PushPlus token
        title: 推送标题
        content: HTML 内容
        topic: 群组编码(可选)
    """
    payload = {
        "token": token,
        "title": title,
        "content": content,
        "template": "html",
    }
    if topic:
        payload["topic"] = topic

    try:
        resp = requests.post(PUSHPLUS_URL, json=payload, timeout=15)
        result = resp.json()
        if result.get("code") == 200:
            logger.info("PushPlus 推送成功: %s", title)
        else:
            logger.error("PushPlus 推送失败: %s", result)
        return result
    except Exception as exc:
        logger.exception("PushPlus 请求异常: %s", exc)
        return {"code": -1, "msg": str(exc)}
