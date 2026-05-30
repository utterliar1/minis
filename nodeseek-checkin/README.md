# NodeSeek 自动签到脚本

自动签到 [NodeSeek](https://www.nodeseek.com/) 论坛，使用 `curl_cffi` 模拟 Chrome TLS 指纹绕过 Cloudflare。

## 功能特性

- ✅ 自动签到获取鸡腿
- 📊 查询签到收益统计（近30天）
- 🔀 随机/固定延迟签到
- 🎲 随机鸡腿模式
- 🌐 支持 HTTP/HTTPS 代理
- 🔔 集成青龙面板通知推送

## 环境要求

- Python 3.8+
- curl_cffi

## 安装依赖

```bash
pip install curl_cffi
```

## 环境变量

```bash
# 必填：NodeSeek Cookie
export NODESEEK_COOKIE="colorscheme=light; session=xxx; cf_clearance=xxx; smac=xxx; fog=xxx; hmti_=xxx"

# 可选：代理地址
export NODESEEK_PROXY_URL="http://192.168.3.4:7890"

# 可选：签到模式（true=随机鸡腿，false=固定5鸡腿）
export NODESEEK_SIGN_RANDOM="true"

# 可选：固定延迟（秒）
export NODESEEK_FIXED_DELAY="0"

# 可选：随机延迟范围（秒）
export NODESEEK_RANDOM_DELAY_MIN="0"
export NODESEEK_RANDOM_DELAY_MAX="0"
```

| 变量 | 必填 | 说明 | 默认值 |
|------|------|------|--------|
| `NODESEEK_COOKIE` | ✅ | 完整 Cookie | - |
| `NODESEEK_PROXY_URL` | ❌ | 代理地址 | - |
| `NODESEEK_SIGN_RANDOM` | ❌ | 随机鸡腿 | `true` |
| `NODESEEK_FIXED_DELAY` | ❌ | 固定延迟秒数 | `0` |
| `NODESEEK_RANDOM_DELAY_MIN` | ❌ | 随机延迟最小值 | `0` |
| `NODESEEK_RANDOM_DELAY_MAX` | ❌ | 随机延迟最大值 | `0` |

## 获取 Cookie

1. 登录 https://www.nodeseek.com
2. 按 `F12` 打开开发者工具
3. 切换到 `Network` 标签
4. 刷新页面，点击任意请求
5. 复制 `Request Headers` 中的完整 Cookie

**Cookie 必须包含以下字段：**
- `session`
- `cf_clearance`
- `smac`
- `fog`
- `hmti_`

## 青龙面板部署

1. 将 `nodeseek-checkin.py` 和 `notify.py` 放到青龙脚本目录
2. 添加环境变量（见上方）
3. 添加定时任务：`23 14 * * *`

## 项目结构

```
nodeseek-checkin/
├── nodeseek-checkin.py              # 主签到脚本
├── notify.py                        # 通知推送模块
├── nodeseek-cookie-extractor.user.js # 猴油脚本（辅助获取 Cookie）
├── .env.example                     # 环境变量示例
├── .gitignore
└── README.md
```

## 免责声明

本脚本仅供学习交流使用，请遵守 NodeSeek 论坛规则。