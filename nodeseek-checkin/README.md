# NodeSeek 自动签到脚本

自动签到 [NodeSeek](https://www.nodeseek.com/) 论坛，支持多账号、随机延迟、青龙面板通知推送。

## 功能特性

- ✅ 自动签到获取鸡腿
- 📊 查询签到收益统计（近30天）
- 🔀 随机延迟签到（避免被检测）
- 📱 多账号支持
- 🔔 集成青龙面板通知推送（sendNotify.js）

## 环境要求

- Node.js >= 18.0.0

## 使用方法

### 1. 获取 Cookie

1. 登录 [https://www.nodeseek.com](https://www.nodeseek.com)
2. 按 `F12` 打开开发者工具
3. 切换到 `Network` 标签
4. 刷新页面，找到任意请求
5. 在 `Request Headers` 中复制 `Cookie` 值

### 2. 配置通知（可选）

将青龙面板的 `sendNotify.js` 放到脚本同目录下，即可自动启用通知推送。

支持的通知方式：
- 微信 Server酱
- Bark App
- Telegram 机器人
- 钉钉机器人
- 企业微信机器人
- iGot
- pushplus

### 3. 设置环境变量

#### Windows (PowerShell)

```powershell
# 单个账号
$env:NODESEEK_COOKIE="your_cookie_here"

# 多个账号（用换行分隔）
$env:NODESEEK_COOKIE="cookie1`ncookie2"
```

#### Windows (CMD)

```cmd
set NODESEEK_COOKIE=your_cookie_here
```

#### Linux/macOS

```bash
export NODESEEK_COOKIE="your_cookie_here"

# 多个账号
export NODESEEK_COOKIE="cookie1
cookie2"
```

### 4. 运行脚本

```bash
node nodeseek-checkin.js
```

## 环境变量说明

| 变量名 | 必填 | 说明 | 默认值 |
|--------|------|------|--------|
| `NODESEEK_COOKIE` | ✅ | NodeSeek Cookie（多账号用换行分隔） | - |
| `RANDOM_SIGNIN` | ❌ | 是否启用随机延迟签到 | `true` |
| `MAX_RANDOM_DELAY` | ❌ | 最大随机延迟秒数 | `3600` |

通知相关的环境变量请参考 `sendNotify.js` 的配置。

## 定时任务

### Windows (任务计划程序)

1. 打开"任务计划程序"
2. 创建基本任务
3. 设置触发器（如每天 14:00）
4. 操作选择"启动程序"
5. 程序填写 `node`
6. 参数填写 `D:\path\to\nodeseek-checkin.js`
7. 起始于填写脚本所在目录

### Linux (Crontab)

```bash
# 编辑 crontab
crontab -e

# 添加定时任务（每天 14:23 执行）
23 14 * * * NODESEEK_COOKIE="your_cookie" /usr/bin/node /path/to/nodeseek-checkin.js
```

### 青龙面板

1. 添加订阅或脚本
2. 环境变量中添加 `NODESEEK_COOKIE`
3. 定时规则: `23 14 * * *`

## 常见问题

### Q: 提示"Cookie 无效或已过期"

A: Cookie 可能已过期，重新登录网站获取新的 Cookie。

### Q: 如何同时签到多个账号？

A: 在 `NODESEEK_COOKIE` 中用换行分隔多个 Cookie。

### Q: 随机延迟有什么用？

A: 避免所有账号在同一时间签到，降低被检测的风险。

### Q: 如何启用通知？

A: 将青龙面板的 `sendNotify.js` 放到脚本同目录下即可自动启用。

## 免责声明

本脚本仅供学习交流使用，请遵守 NodeSeek 论坛规则。使用本脚本产生的一切后果由使用者自行承担。