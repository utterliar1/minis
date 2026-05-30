#!/usr/bin/env node

/**
 * NodeSeek 自动签到脚本
 * 
 * 使用方法:
 * 1. 设置环境变量 NODESEEK_COOKIE（多个账号用换行分隔）
 * 2. 将青龙面板的 sendNotify.js 放到同目录下（可选，用于推送通知）
 * 3. 运行: node nodeseek-checkin.js
 * 
 * 环境变量说明:
 * - NODESEEK_COOKIE: NodeSeek 的 Cookie（必填，多个账号用换行分隔）
 * - RANDOM_SIGNIN: 是否启用随机延迟签到（默认 true）
 * - MAX_RANDOM_DELAY: 最大随机延迟秒数（默认 3600）
 */

const https = require('https');
const { URL } = require('url');

// ==================== 通知模块 ====================
let sendNotify = null;
try {
  sendNotify = require('./sendNotify').sendNotify;
  console.log('✅ 已加载青龙通知模块');
} catch (e) {
  console.log('⚠️  未找到 sendNotify.js，通知功能已禁用');
}

// ==================== 配置 ====================
const CONFIG = {
  randomSignin: (process.env.RANDOM_SIGNIN || 'true').toLowerCase() !== 'false',
  maxRandomDelay: parseInt(process.env.MAX_RANDOM_DELAY || '3600'),
};

// ==================== 工具函数 ====================

function request(url, options = {}) {
  return new Promise((resolve, reject) => {
    const urlObj = new URL(url);
    const reqOptions = {
      hostname: urlObj.hostname,
      port: 443,
      path: urlObj.pathname + urlObj.search,
      method: options.method || 'GET',
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36 Edg/125.0.0.0',
        'Origin': 'https://www.nodeseek.com',
        'Referer': 'https://www.nodeseek.com/board',
        'Content-Type': 'application/json',
        ...options.headers,
      },
    };

    const req = https.request(reqOptions, (res) => {
      let data = '';
      res.on('data', (chunk) => { data += chunk; });
      res.on('end', () => {
        try {
          resolve({ status: res.statusCode, data: JSON.parse(data) });
        } catch {
          resolve({ status: res.statusCode, data });
        }
      });
    });

    req.on('error', reject);
    req.setTimeout(15000, () => {
      req.destroy();
      reject(new Error('请求超时'));
    });

    if (options.body) {
      req.write(JSON.stringify(options.body));
    }
    req.end();
  });
}

function formatTime(seconds) {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const parts = [];
  if (h > 0) parts.push(`${h}小时`);
  if (m > 0) parts.push(`${m}分钟`);
  if (s > 0 || parts.length === 0) parts.push(`${s}秒`);
  return parts.join('');
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

// ==================== 核心功能 ====================

async function signIn(cookie) {
  const random = CONFIG.randomSignin ? randomInt(100000, 999999) : 'false';
  const url = `https://www.nodeseek.com/api/attendance?random=${random}`;

  try {
    const response = await request(url, {
      method: 'POST',
      headers: { Cookie: cookie },
    });

    const { status, data } = response;
    const msg = data?.message || '';

    if (msg.includes('鸡腿') || data?.success) {
      return { result: 'success', msg };
    } else if (msg.includes('已完成签到')) {
      return { result: 'already', msg };
    } else if (status === 404 || data?.status === 404) {
      return { result: 'invalid', msg: msg || 'Cookie 无效或已过期' };
    }
    return { result: 'fail', msg: msg || '签到失败' };
  } catch (error) {
    return { result: 'error', msg: error.message };
  }
}

async function getSigninStats(cookie, days = 30) {
  const headers = { Cookie: cookie };
  const allRecords = [];

  try {
    for (let page = 1; page <= 10; page++) {
      const url = `https://www.nodeseek.com/api/account/credit/page-${page}`;
      const response = await request(url, { headers });

      if (!response.data?.success || !response.data?.data?.length) break;

      const records = response.data.data;
      allRecords.push(...records);

      const lastTime = new Date(records[records.length - 1][3]);
      const startTime = new Date(Date.now() - days * 24 * 60 * 60 * 1000);
      if (lastTime < startTime) break;

      await sleep(500);
    }

    const startTime = new Date(Date.now() - days * 24 * 60 * 60 * 1000);
    const signinRecords = allRecords
      .filter(record => {
        const [amount, balance, description, timestamp] = record;
        const recordTime = new Date(timestamp);
        return recordTime >= startTime && description.includes('签到收益') && description.includes('鸡腿');
      })
      .map(record => ({
        amount: record[0],
        date: new Date(record[3]).toISOString().split('T')[0],
        description: record[2],
      }));

    if (signinRecords.length === 0) {
      return {
        totalAmount: 0,
        average: 0,
        daysCount: 0,
        period: days === 1 ? '今天' : `近${days}天`,
      };
    }

    const totalAmount = signinRecords.reduce((sum, r) => sum + r.amount, 0);
    return {
      totalAmount,
      average: (totalAmount / signinRecords.length).toFixed(2),
      daysCount: signinRecords.length,
      period: days === 1 ? '今天' : `近${days}天`,
    };
  } catch (error) {
    console.error('查询签到统计失败:', error.message);
    return null;
  }
}

async function waitWithCountdown(seconds, accountName) {
  let remaining = seconds;
  process.stdout.write('\n');

  while (remaining > 0) {
    const timeStr = formatTime(remaining);
    process.stdout.write(`\r${accountName} 倒计时: ${timeStr}  `);

    const sleepTime = remaining <= 10 ? 1 : Math.min(10, remaining);
    await sleep(sleepTime * 1000);
    remaining -= sleepTime;
  }
  process.stdout.write('\r' + ' '.repeat(50) + '\r');
}

// ==================== 主流程 ====================

async function main() {
  console.log('=== NodeSeek 自动签到脚本 ===\n');

  const allCookies = process.env.NODESEEK_COOKIE || '';
  const cookieList = allCookies
    .split(/[\n\r]+/)
    .map(c => c.trim())
    .filter(Boolean);

  console.log(`共发现 ${cookieList.length} 个账号`);
  console.log(`随机签到: ${CONFIG.randomSignin ? '启用' : '禁用'}`);

  if (cookieList.length === 0) {
    console.error('\n❌ 未找到 Cookie，请设置 NODESEEK_COOKIE 环境变量');
    console.log('\n获取 Cookie 方法:');
    console.log('1. 登录 https://www.nodeseek.com');
    console.log('2. 按 F12 打开开发者工具');
    console.log('3. 切换到 Network 标签');
    console.log('4. 刷新页面，找到任意请求');
    console.log('5. 在 Request Headers 中复制 Cookie 值');
    process.exit(1);
  }

  const signinSchedule = [];
  const now = new Date();

  if (CONFIG.randomSignin) {
    console.log(`\n随机签到时间窗口: ${Math.floor(CONFIG.maxRandomDelay / 60)} 分钟`);
    console.log('\n==== 生成签到时间表 ====');

    cookieList.forEach((cookie, i) => {
      const delay = randomInt(0, CONFIG.maxRandomDelay);
      const signinTime = new Date(now.getTime() + delay * 1000);

      signinSchedule.push({
        accountIndex: i + 1,
        displayName: `账号${i + 1}`,
        cookie,
        delaySeconds: delay,
        signinTime,
      });

      console.log(`账号${i + 1}: 延迟 ${formatTime(delay)} 后签到 (预计 ${signinTime.toLocaleTimeString('zh-CN')})`);
    });

    signinSchedule.sort((a, b) => a.delaySeconds - b.delaySeconds);

    console.log('\n==== 签到执行顺序 ====');
    signinSchedule.forEach(item => {
      console.log(`${item.displayName}: ${item.signinTime.toLocaleTimeString('zh-CN')}`);
    });
  } else {
    cookieList.forEach((cookie, i) => {
      signinSchedule.push({
        accountIndex: i + 1,
        displayName: `账号${i + 1}`,
        cookie,
        delaySeconds: 0,
        signinTime: now,
      });
    });
  }

  console.log('\n==== 开始执行签到任务 ====');

  const results = [];

  for (const item of signinSchedule) {
    const { displayName, cookie, delaySeconds } = item;

    if (delaySeconds > 0) {
      await waitWithCountdown(delaySeconds, displayName);
    }

    console.log(`\n==== ${displayName} 开始签到 ====`);
    console.log(`当前时间: ${new Date().toLocaleTimeString('zh-CN')}`);

    const { result, msg } = await signIn(cookie);

    if (result === 'success' || result === 'already') {
      console.log(`✅ ${displayName} 签到成功: ${msg}`);

      console.log('正在查询签到收益统计...');
      const stats = await getSigninStats(cookie, 30);
      if (stats) {
        console.log(`📊 ${stats.period}已签到${stats.daysCount}天，共获得${stats.totalAmount}个鸡腿，平均${stats.average}个/天`);
      }

      results.push({ account: displayName, success: true, msg, stats });
    } else {
      console.log(`❌ ${displayName} 签到失败: ${msg}`);
      results.push({ account: displayName, success: false, msg });
    }
  }

  // 发送汇总通知（使用青龙 sendNotify）
  if (sendNotify) {
    const successCount = results.filter(r => r.success).length;
    const failCount = results.length - successCount;

    let notifyTitle = 'NodeSeek 签到结果';
    let notifyContent = `共 ${results.length} 个账号\n`;
    notifyContent += `✅ 成功: ${successCount}\n`;
    if (failCount > 0) notifyContent += `❌ 失败: ${failCount}\n`;

    results.forEach(r => {
      notifyContent += `\n${r.account}: ${r.success ? '✅' : '❌'} ${r.msg}`;
      if (r.stats) {
        notifyContent += `\n  ${r.stats.period}签到${r.stats.daysCount}天，共${r.stats.totalAmount}鸡腿`;
      }
    });

    try {
      await sendNotify(notifyTitle, notifyContent);
      console.log('\n📱 通知已发送');
    } catch (e) {
      console.error('\n❌ 通知发送失败:', e.message);
    }
  }

  console.log('\n==== 所有账号签到完成 ====');
  console.log(`完成时间: ${new Date().toLocaleString('zh-CN')}`);
}

main().catch(error => {
  console.error('程序执行出错:', error);
  process.exit(1);
});