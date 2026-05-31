#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import asyncio
import traceback

from telethon import Button, events
from random import randrange
from asyncio import exceptions

from jbot import BOT_SET, ch_name, chat_id, jdbot, logger, row, TASK_CMD
from jbot.bot.utils import press_event, execute


@jdbot.on(events.NewMessage(from_users=chat_id, pattern=r'^/xiaomi$'))
async def xiaomi(event):
    """小米运动刷步数 - 通过TG机器人选择步数并执行"""
    SENDER = event.sender_id
    step1 = randrange(1000, 3000, 1)
    step2 = randrange(3000, 5000, 1)
    step3 = randrange(5000, 8000, 1)
    step4 = randrange(8000, 10000, 1)
    step5 = randrange(10000, 20000, 1)
    try:
        cmdtext = None
        async with jdbot.conversation(SENDER, timeout=180) as conv:
            btn = [
                [Button.inline(f'{step1}', data=f'{step1}'),
                 Button.inline(f'{step2}', data=f'{step2}'),
                 Button.inline(f'{step3}', data=f'{step3}')],
                [Button.inline(f'{step4}', data=f'{step4}'),
                 Button.inline(f'{step5}', data=f'{step5}'),
                 Button.inline('取消', data='cancel')]
            ]
            msg = await jdbot.send_message(chat_id, '请选择想要改成的步数：', buttons=btn)
            convdata = await conv.wait_event(press_event(SENDER))
            res = bytes.decode(convdata.data)
            if res == 'cancel':
                msg = await jdbot.edit_message(msg, '对话已取消')
                conv.cancel()
            else:
                await jdbot.delete_messages(chat_id, msg)
                cmdtext = f'export STEP="{res}" && {TASK_CMD} xmsport.py'
                conv.cancel()
        if cmdtext:
            info = '开始刷步数'
            msg = await jdbot.send_message(chat_id, info)
            info += f'\n本次步数为`{res}`'
            await execute(msg, info, cmdtext)
    except exceptions.TimeoutError:
        msg = await jdbot.send_message(chat_id, '选择已超时，对话已停止')
    except Exception as e:
        await jdbot.send_message(chat_id, f'something wrong\n{str(e)}')
        logger.error(f'something wrong\n{str(e)}')


if ch_name:
    jdbot.add_event_handler(
        xiaomi,
        events.NewMessage(
            chats=chat_id,
            from_users=chat_id,
            pattern=BOT_SET.get('命令别名', {}).get('xiaomi', '/xiaomi')
        )
    )
