# 考勤助手

GPS 地理围栏打卡 + 团队管理 + 工时统计，单容器 Docker 部署。

## 功能

### 管理员
- 📊 **团队概览**：实时查看所有成员打卡状态、今日/本周/本月工时
- 📈 **工时趋势**：近 30 天团队工时趋势柱状图
- 🏆 **工时排行**：本月工时排行，含团队平均值对比
- 👥 **成员管理**：白名单注册、角色切换、密码重置
- 📧 **邮件报告**：SMTP 配置、定时发送工时报表
- 📤 **导出报表**：按成员/时段导出 CSV，支持自定义日期范围
- ⚙️ **系统设置**：打卡位置、工作时间、节假日同步、修改密码

### 普通用户
- 📍 **GPS 打卡**：地理围栏验证，支持范围外打卡标记
- 📅 **日历记录**：点击日期查看详情，时间线展示打卡内容
- 📈 **工时统计**：今日/本周/本月/累计工时，月度趋势图
- 📤 **个人导出**：导出个人工时报表

## 技术栈

| 组件 | 技术 |
|------|------|
| 后端 | Python Flask + SQLite + JWT |
| 前端 | 原生 HTML/CSS/JS（单文件） |
| 服务器 | Nginx + Gunicorn（单容器） |
| 部署 | Docker Compose |

## 快速开始

```bash
# 克隆
git clone https://github.com/utterliar1/minis.git
cd minis/overtime-tracker

# 构建并启动
docker compose up -d --build

# 访问
open http://localhost:3090
```

默认管理员账号：`admin` / `admin123`

## 目录结构

```
overtime-tracker/
├── Dockerfile          # Python Alpine + Nginx + Gunicorn
├── docker-compose.yml  # 端口 3090，数据挂载到 ./data
├── nginx.conf          # 反代 API + 静态文件
├── start.sh            # 容器启动脚本
├── .gitignore          # 排除 data/ 和 *.db
├── backend/
│   ├── app.py          # Flask 后端 API
│   └── requirements.txt
└── frontend/
    └── index.html      # 单页面前端
```

## 数据存储

- SQLite 数据库：`./data/overtime.db`
- Docker 挂载卷，方便迁移备份
- 所有数据（用户、打卡记录、设置、白名单）存服务器端

## 使用流程

### 管理员初始化

1. 登录管理员账号
2. 设置打卡位置（点击「📍 使用当前位置」自动获取）
3. 配置工作时间（默认 08:30 - 17:30）
4. 同步法定节假日
5. 添加成员白名单
6. （可选）配置邮件定时发送报告

### 成员注册与打卡

1. 管理员在白名单中添加成员姓名
2. 成员用姓名 + 密码注册
3. 到达打卡范围后点击打卡（上班打卡需要填写事由，下班打卡不需要重复填写事由）

## 导出功能

- **人员选择**：所有人 / 指定成员
- **时间范围**：今日 / 本周 / 本月 / 全部 / 自定义日期范围
- **导出格式**：CSV（姓名、日期、星期、上班、下班、类型、事由、远程、实际位置、工时），管理员导出所有人时按成员追加小计行，底部保留总计行；范围外记录在“实际位置”列显示经纬度、精度和距离

## API 接口

| 接口 | 方法 | 权限 | 说明 |
|------|------|------|------|
| `/api/login` | POST | 公开 | 登录（支持姓名或用户名） |
| `/api/register` | POST | 公开 | 注册（需白名单） |
| `/api/clock` | POST | 用户 | 打卡 |
| `/api/records` | GET | 用户 | 个人记录 |
| `/api/records/all` | GET | 管理员 | 全部记录 |
| `/api/export` | GET | 用户 | 导出工时（支持 uid/period/from/to） |
| `/api/users` | GET | 管理员 | 用户列表 |
| `/api/users/:id/password` | PUT | 管理员 | 重置密码 |
| `/api/change-password` | PUT | 用户 | 修改自己的密码 |
| `/api/whitelist` | GET/POST | 管理员 | 白名单管理 |
| `/api/settings` | GET/PUT | 管理员 | 系统设置 |
| `/api/email-config` | GET/PUT | 管理员 | 邮件配置 |

## 节假日同步

自动从 [NateScarlet/holiday-cn](https://github.com/NateScarlet/holiday-cn) 获取当年中国法定节假日和调休工作日。

工时计算优先级：
1. 法定节假日 → 休息日，全天计入工时
2. 调休补班 → 工作日，标准工作时间外的记录时长计入工时
3. 默认工作日 → 按周一至周五

## 部署到服务器

```bash
# 上传到服务器
scp -r overtime-tracker/ root@your-server:/root/

# 构建启动
ssh root@your-server 'cd /root/overtime-tracker && docker compose up -d --build'

# 配置反代指向 localhost:3090
```

## 许可

MIT
