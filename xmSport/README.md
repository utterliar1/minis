# xmSport

小米运动健康刷步数脚本，适用于青龙面板。

## 环境变量

| 变量 | 必填 | 说明 |
|------|:----:|------|
| `MI_USER` | ✅ | 小米账号手机号 |
| `MI_PWD` | ✅ | 小米账号密码 |
| `STEP` | ❌ | 步数或范围，如 `18000-20000`，默认随机 18000-22000 |

## 青龙面板使用

1. 将 `xmsport.py` 和 `data.txt` 放入青龙脚本目录
2. 在青龙面板添加环境变量 `MI_USER` 和 `MI_PWD`
3. 添加定时任务，命令为 `task xmsport.py`

## Telegram 机器人

配合 jbot 的 `/xiaomi` 命令使用，可手动选择步数执行。

## 数据来源

基于 [chiupam/xmSport](https://github.com/chiupam/xmSport) 项目，使用最新版 API 逻辑（MiFit/6.12.0）重写为 Python 版本。
