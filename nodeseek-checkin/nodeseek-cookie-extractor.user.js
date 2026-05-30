// ==UserScript==
// @name         NodeSeek Cookie 提取器
// @namespace    https://github.com/utterliar1
// @version      1.2.0
// @description  登录 NodeSeek 后提示如何获取完整 Cookie
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
      max-width: 400px;
      min-width: 320px;
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
    }
    #ns-cookie-panel .close-btn:hover { color: #333; }
    #ns-cookie-panel .steps {
      background: #f5f5f5;
      border-radius: 8px;
      padding: 12px;
      font-size: 13px;
      line-height: 1.8;
      color: #555;
    }
    #ns-cookie-panel .steps ol {
      margin: 0;
      padding-left: 20px;
    }
    #ns-cookie-panel .btn-group {
      display: flex;
      gap: 8px;
      margin-top: 12px;
    }
    #ns-cookie-panel .btn {
      flex: 1;
      padding: 8px 12px;
      border: none;
      border-radius: 6px;
      font-size: 13px;
      cursor: pointer;
    }
    #ns-cookie-panel .btn-primary {
      background: #4CAF50;
      color: #fff;
    }
    #ns-cookie-panel .btn-primary:hover { background: #45a049; }
    #ns-cookie-panel .btn-secondary {
      background: #e0e0e0;
      color: #333;
    }
    #ns-cookie-panel .hint {
      font-size: 11px;
      color: #888;
      margin-top: 10px;
      text-align: center;
    }
    #ns-cookie-panel .status {
      margin-top: 8px;
      font-size: 12px;
      color: #4CAF50;
      text-align: center;
    }
  `);

  function createPanel() {
    const panel = document.createElement('div');
    panel.id = 'ns-cookie-panel';
    panel.innerHTML = `
      <div class="header">
        <span class="title">🍪 获取 NodeSeek Cookie</span>
        <button class="close-btn" id="ns-close-btn">✕</button>
      </div>
      <div class="steps">
        <ol>
          <li>按 <b>F12</b> 打开开发者工具</li>
          <li>切换到 <b>Network</b> 标签</li>
          <li>刷新页面</li>
          <li>点击任意一个请求</li>
          <li>找到 <b>Request Headers</b> 中的 <b>Cookie</b></li>
          <li>复制完整 Cookie 值</li>
        </ol>
      </div>
      <div class="hint">⚠️ 必须包含: <b>session</b>, <b>cf_clearance</b>, <b>smac</b>, <b>fog</b>, <b>hmti_</b></div>
      <div class="btn-group">
        <button class="btn btn-primary" id="ns-copy-doc-cookie">📋 复制 document.cookie</button>
        <button class="btn btn-secondary" id="ns-close-btn2">关闭</button>
      </div>
      <div class="status" id="ns-status"></div>
    `;
    return panel;
  }

  function showPanel() {
    const old = document.getElementById('ns-cookie-panel');
    if (old) old.remove();

    const panel = createPanel();
    document.body.appendChild(panel);

    panel.querySelector('#ns-close-btn').addEventListener('click', () => panel.remove());
    panel.querySelector('#ns-close-btn2').addEventListener('click', () => panel.remove());

    panel.querySelector('#ns-copy-doc-cookie').addEventListener('click', () => {
      const cookie = document.cookie;
      if (cookie) {
        GM_setClipboard(cookie, 'text');
        const status = panel.querySelector('#ns-status');
        status.textContent = '✅ 已复制（注意：可能缺少 HttpOnly Cookie，建议手动从 Network 复制）';
        GM_notification({ title: 'NodeSeek Cookie', text: '已复制到剪贴板', timeout: 2000 });
      }
    });
  }

  if (document.readyState === 'complete') {
    setTimeout(showPanel, 500);
  } else {
    window.addEventListener('load', () => setTimeout(showPanel, 500));
  }
})();