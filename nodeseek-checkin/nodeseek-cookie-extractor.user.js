// ==UserScript==
// @name         NodeSeek Cookie 提取器
// @namespace    https://github.com/utterliar1
// @version      1.0.0
// @description  登录 NodeSeek 后自动提取 Cookie，方便复制给签到脚本使用
// @author       utterliar1
// @match        https://www.nodeseek.com/*
// @match        https://nodeseek.com/*
// @grant        GM_setClipboard
// @grant        GM_notification
// @grant        GM_addStyle
// @run-at       document-idle
// ==/UserScript==

(function () {
  'use strict';

  // ==================== 样式 ====================
  GM_addStyle(`
    #ns-cookie-panel {
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 99999;
      background: #fff;
      border: 2px solid #4CAF50;
      border-radius: 12px;
      padding: 16px;
      box-shadow: 0 4px 20px rgba(0,0,0,0.15);
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
      font-size: 14px;
      max-width: 360px;
      min-width: 280px;
      transition: all 0.3s ease;
    }
    #ns-cookie-panel.collapsed {
      min-width: auto;
      max-width: auto;
      padding: 10px 14px;
      cursor: pointer;
    }
    #ns-cookie-panel .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
    }
    #ns-cookie-panel .title {
      font-size: 16px;
      font-weight: 600;
      color: #333;
    }
    #ns-cookie-panel .close-btn {
      cursor: pointer;
      font-size: 18px;
      color: #999;
      background: none;
      border: none;
      padding: 0 4px;
      line-height: 1;
    }
    #ns-cookie-panel .close-btn:hover {
      color: #333;
    }
    #ns-cookie-panel .cookie-box {
      background: #f5f5f5;
      border: 1px solid #ddd;
      border-radius: 8px;
      padding: 10px;
      word-break: break-all;
      font-family: 'Courier New', monospace;
      font-size: 12px;
      color: #555;
      max-height: 100px;
      overflow-y: auto;
      margin-bottom: 12px;
    }
    #ns-cookie-panel .btn-group {
      display: flex;
      gap: 8px;
    }
    #ns-cookie-panel .btn {
      flex: 1;
      padding: 8px 12px;
      border: none;
      border-radius: 6px;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
    }
    #ns-cookie-panel .btn-primary {
      background: #4CAF50;
      color: #fff;
    }
    #ns-cookie-panel .btn-primary:hover {
      background: #45a049;
    }
    #ns-cookie-panel .btn-secondary {
      background: #e0e0e0;
      color: #333;
    }
    #ns-cookie-panel .btn-secondary:hover {
      background: #d0d0d0;
    }
    #ns-cookie-panel .status {
      margin-top: 10px;
      font-size: 12px;
      color: #666;
      text-align: center;
    }
    #ns-cookie-panel .status.success {
      color: #4CAF50;
    }
    #ns-cookie-panel .status.error {
      color: #f44336;
    }
    #ns-cookie-panel .toggle-icon {
      font-size: 20px;
      cursor: pointer;
    }
  `);

  // ==================== 主逻辑 ====================

  // 检查是否已登录
  function isLoggedIn() {
    // 检查常见的登录状态标识
    const cookie = document.cookie;
    return cookie && cookie.includes('session');
  }

  // 提取完整的 Cookie
  function getCookie() {
    return document.cookie;
  }

  // 创建浮动面板
  function createPanel() {
    const panel = document.createElement('div');
    panel.id = 'ns-cookie-panel';

    const cookie = getCookie();
    const loggedIn = isLoggedIn();

    panel.innerHTML = `
      <div class="header">
        <span class="title">🍪 NodeSeek Cookie</span>
        <button class="close-btn" id="ns-close-btn">✕</button>
      </div>
      ${loggedIn ? `
        <div class="cookie-box" id="ns-cookie-box">${cookie || '未检测到 Cookie'}</div>
        <div class="btn-group">
          <button class="btn btn-primary" id="ns-copy-btn">📋 复制 Cookie</button>
          <button class="btn btn-secondary" id="ns-hide-btn">🙈 隐藏</button>
        </div>
        <div class="status" id="ns-status"></div>
      ` : `
        <div class="status error">⚠️ 未检测到登录状态，请先登录</div>
        <div class="btn-group">
          <button class="btn btn-secondary" id="ns-refresh-btn">🔄 刷新检测</button>
          <button class="btn btn-secondary" id="ns-hide-btn">🙈 隐藏</button>
        </div>
      `}
    `;

    return panel;
  }

  // 绑定事件
  function bindEvents(panel) {
    // 关闭按钮
    const closeBtn = panel.querySelector('#ns-close-btn');
    if (closeBtn) {
      closeBtn.addEventListener('click', () => {
        panel.remove();
      });
    }

    // 复制按钮
    const copyBtn = panel.querySelector('#ns-copy-btn');
    if (copyBtn) {
      copyBtn.addEventListener('click', () => {
        const cookie = getCookie();
        if (cookie) {
          GM_setClipboard(cookie, 'text');
          const status = panel.querySelector('#ns-status');
          status.textContent = '✅ 已复制到剪贴板！';
          status.className = 'status success';

          GM_notification({
            title: 'NodeSeek Cookie',
            text: 'Cookie 已复制到剪贴板',
            timeout: 2000
          });

          setTimeout(() => {
            status.textContent = '';
            status.className = 'status';
          }, 3000);
        }
      });
    }

    // 隐藏按钮
    const hideBtn = panel.querySelector('#ns-hide-btn');
    if (hideBtn) {
      hideBtn.addEventListener('click', () => {
        panel.classList.add('collapsed');
        panel.innerHTML = '<span class="toggle-icon">🍪</span>';
        panel.addEventListener('click', () => {
          panel.classList.remove('collapsed');
          panel.remove();
          showPanel();
        });
      });
    }

    // 刷新按钮
    const refreshBtn = panel.querySelector('#ns-refresh-btn');
    if (refreshBtn) {
      refreshBtn.addEventListener('click', () => {
        panel.remove();
        showPanel();
      });
    }
  }

  // 显示面板
  function showPanel() {
    // 移除旧面板
    const oldPanel = document.getElementById('ns-cookie-panel');
    if (oldPanel) oldPanel.remove();

    const panel = createPanel();
    document.body.appendChild(panel);
    bindEvents(panel);
  }

  // ==================== 启动 ====================

  // 页面加载完成后显示面板
  if (document.readyState === 'complete') {
    showPanel();
  } else {
    window.addEventListener('load', showPanel);
  }

  // 监听登录状态变化（通过 Cookie 变化）
  let lastCookie = document.cookie;
  setInterval(() => {
    const currentCookie = document.cookie;
    if (currentCookie !== lastCookie) {
      lastCookie = currentCookie;
      const panel = document.getElementById('ns-cookie-panel');
      if (panel) {
        panel.remove();
        showPanel();
      }
    }
  }, 1000);

})();