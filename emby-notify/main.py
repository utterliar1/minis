"""emby-notify - Telegram 新入库消息监控 & PushPlus 微信推送.

用法:
    1. 复制 config.json.example 为 config.json 并填写配置
    2. pip install -r requirements.txt
    3. python main.py
"""

import asyncio
import json
import logging
from pathlib import Path

from telethon import TelegramClient, events
from telethon.tl.types import MessageMediaPhoto

from parser import parse_media_message, build_title
from pushplus import build_html, send

CONFIG_PATH = Path(__file__).parent / "config.json"

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger(__name__)


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(
            f"配置文件不存在: {CONFIG_PATH}\n"
            "请复制 config.json.example 为 config.json 并填写配置"
        )
    with open(CONFIG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


async def download_photo(client: TelegramClient, message) -> str:
    """下载消息中的封面图片并返回 base64 data URI, 失败返回空串."""
    try:
        if message.media and isinstance(message.media, MessageMediaPhoto):
            import base64
            import io

            photo_bytes = await client.download_media(message, file=bytes)
            if photo_bytes:
                b64 = base64.b64encode(photo_bytes).decode()
                return f'<img src="data:image/jpeg;base64,{b64}" style="max-width:100%;border-radius:8px;margin-bottom:16px;" />'
    except Exception as exc:
        logger.warning("封面下载失败: %s", exc)
    return ""


async def main():
    cfg = load_config()

    client = TelegramClient(
        cfg.get("session_name", "emby_notify"),
        cfg["api_id"],
        cfg["api_hash"],
    )

    pushplus_token = cfg["pushplus_token"]
    pushplus_topic = cfg.get("pushplus_topic", "")
    monitor_chats = cfg.get("monitor_chats", [])
    include_cover = cfg.get("include_cover", True)

    if not monitor_chats:
        logger.error("monitor_chats 为空，请在 config.json 中配置要监听的 chat")
        return

    @client.on(events.NewMessage(chats=monitor_chats))
    async def handler(event):
        text = event.message.text or event.message.message or ""
        info = parse_media_message(text)
        if not info:
            return

        title = build_title(info)
        logger.info("检测到新入库: %s", title)

        cover_html = ""
        if include_cover:
            cover_html = await download_photo(client, event.message)

        html = cover_html + build_html(info)
        send(pushplus_token, title, html, topic=pushplus_topic)

    await client.start()
    logger.info("emby-notify 已启动，监听 %d 个 chat...", len(monitor_chats))
    await client.run_until_disconnected()


if __name__ == "__main__":
    asyncio.run(main())
