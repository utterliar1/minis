# 考勤助手 B 方案设计文档

> 日期：2026-06-08
> 范围：安全加固 + 体验重构 + 前端拆分

## 背景

考勤助手是一个工时记录工具，用于记录法定工时之外的工作时长。使用者自愿打卡，管理者根据报表核算费用。系统只负责记录时长，不涉及金额计算。

**核心约束：**
- 系统保持"考勤/工时/打卡"等中性措辞，避免敏感表述
- 记录不可篡改，争议走线下沟通
- GPS 围栏为默认约束，范围外打卡靠事由说明
- 统一费率，不再区分中段休息扣除逻辑

---

## 一、安全加固

### 1.1 密码哈希：SHA256 → bcrypt

**问题：** SHA256 计算太快，数据库泄露后易被暴力破解。

**方案：**
- 新密码使用 `bcrypt` 哈希存储（cost factor 12）
- 存储格式新增标识：bcrypt 哈希以 `$2b$` 开头，旧 SHA256 哈希为 64 位十六进制字符串
- **透明迁移**：用户登录时，检测到旧格式 → 验证 SHA256 通过 → 用 bcrypt 重新哈希 → 写回数据库。用户无感知，无需全员重置密码
- `users` 表结构不变，`password_hash` 字段兼容两种格式
- 移除 `salt` 字段的依赖（bcrypt 自带 salt），但保留字段以兼容旧记录直到全部迁移完成
- `requirements.txt` 新增 `bcrypt` 依赖

### 1.2 JWT Secret 持久化

**问题：** 当前 `JWT_SECRET` 未设置环境变量时每次启动随机生成，容器重启导致全员掉线。

**方案：**
- 启动时检查环境变量 `JWT_SECRET`，如无则读取 `data/jwt_secret` 文件
- 文件不存在则随机生成 32 字节 hex 写入文件
- `data/` 目录已通过 Docker volume 持久化，secret 文件自然跟随

```python
secret_path = os.path.join(os.path.dirname(DB_PATH), 'jwt_secret')
if os.environ.get('JWT_SECRET'):
    SECRET = os.environ['JWT_SECRET']
elif os.path.exists(secret_path):
    with open(secret_path) as f:
        SECRET = f.read().strip()
else:
    SECRET = secrets.token_hex(32)
    with open(secret_path, 'w') as f:
        f.write(SECRET)
```

---

## 二、用户打卡流程精简

### 2.1 事由字段

- 所有打卡（上班/下班）均需填写事由，保持现状不变
- 超范围打卡时，记录中 `out_of_range=1`，事由仍为必填
- 措辞保持中性，不暗示范围外记录是某种固定工作方式

### 2.2 打卡成功反馈

**现状：** 打卡成功后仅显示 toast 提示。

**改进：**
- 打卡成功后，在打卡区域直接展示本次记录的时间点
- 如果是下班打卡，同时显示本次工时时长
- 用户无需切换到日历页确认结果

### 2.3 进行中状态

- 如果今天已打上班卡但未打下班卡，顶部状态卡片显示"进行中"标识
- 实时显示从上班打卡到现在经过的时长（每分钟更新）
- 让用户清楚当前处于什么状态

---

## 三、管理者仪表盘

### 3.1 数据展示优先级

当前四个模块保留，调整侧重：

1. **团队概览**：每个人本月累计工时用醒目数字展示，管理者打开页面第一眼看到谁的工时最多
2. **工时趋势图**：保留近 30 天柱状图不变
3. **工时排行**：保留，保留团队平均值对比
4. **导出报表**：见下方 3.2

### 3.2 导出报表增强

**新增"范围外"列：**

导出 CSV 格式从 8 列扩展为 9 列：

```
姓名 | 日期 | 星期 | 上班 | 下班 | 类型 | 事由 | 范围外 | 工时
```

- `范围外`列：超范围打卡标记为"是"，正常打卡为空
- 管理者可据此快速筛选范围外打卡记录

### 3.3 概览与导出联动

**现状：** 时间范围筛选（今日/本周/本月）在导出区域，概览区域是固定的本月数据。

**改进：** 概览和导出共享同一个时间范围选择器，切换时两边同步更新。

---

## 四、前端结构拆分

### 4.1 目标

将 83KB 单文件 `index.html` 拆分为模块化结构，每个文件控制在几百行以内。

### 4.2 目录结构

```
frontend/
├── index.html          # 页面结构 + 骨架样式（关键 CSS 内联保证首屏渲染）
├── css/
│   └── style.css       # 所有样式
├── js/
│   ├── app.js          # 入口：初始化、页面路由、全局状态
│   ├── auth.js         # 登录/注册/Token管理
│   ├── clock.js        # 打卡逻辑 + 进行中状态
│   ├── records.js      # 日历和记录展示
│   ├── stats.js        # 统计图表
│   ├── admin.js        # 管理者功能（仪表盘、成员、设置、邮件）
│   └── utils.js        # 公共工具函数（toast、modal、时间格式化、API封装等）
├── manifest.json       # 不变
├── sw.js               # 不变
└── icon-*.png          # 不变
```

### 4.3 约束

- 不引入构建工具或框架，保持原生 JS
- 通过 `<script>` 标签按顺序加载：`utils.js` → `auth.js` → `clock.js` → `records.js` → `stats.js` → `admin.js` → `app.js`
- 使用 `window.OT` 命名空间避免全局污染（OT = overtime-tracker 的缩写，不含敏感词）
- Nginx 配置无需改动，继续静态文件服务
- 保留 `index-local.html` 作为单文件备份版本

### 4.4 公共 API 约定

各模块通过 `window.OT` 暴露公共接口：

```javascript
window.OT = {
    // utils.js 提供
    api: Function,           // API 请求封装
    showToast: Function,     // toast 提示
    showModal: Function,     // 弹窗
    formatMinutes: Function, // 分钟格式化
    escapeHtml: Function,    // XSS 防护
    // auth.js 提供
    token: String,           // 当前 token
    user: Object,            // 当前用户信息
    // 各模块自行注册
};
```

`app.js` 在 DOMContentLoaded 时调用各模块的 `init()` 方法完成初始化。

---

## 五、不改动的部分

以下保持现状，不在本次范围内：

- 后端 API 接口定义（除了安全相关的 bcrypt 迁移）
- 数据库表结构
- Docker/Nginx 部署配置
- 节假日同步逻辑
- 邮件报告功能
- 白名单注册机制

---

## 六、验收标准

1. 容器重启后用户登录状态不丢失
2. 新注册用户密码使用 bcrypt 存储
3. 旧用户登录后密码自动迁移为 bcrypt
4. 打卡成功后页面直接展示本次记录
5. 进行中的打卡状态实时显示已用时长
6. 导出 CSV 包含"范围外"列
7. 管理者概览与导出共享时间范围
8. 前端拆分后功能与现有完全一致，无回归
9. 系统使用中性措辞
