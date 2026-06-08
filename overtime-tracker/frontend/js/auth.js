// Authentication and app entry
var OT = window.OT = window.OT || {};

OT.switchAuthTab = function switchAuthTab(t){document.querySelectorAll('.auth-tab').forEach((x,i)=>x.classList.toggle('active',t==='login'?i===0:i===1));document.getElementById('form-login').classList.toggle('active',t==='login');document.getElementById('form-register').classList.toggle('active',t==='register');document.getElementById('login-error').textContent='';document.getElementById('reg-error').textContent=''};

OT.doLogin = async function doLogin(){
  const u=document.getElementById('login-user').value.trim(),p=document.getElementById('login-pass').value,err=document.getElementById('login-error');
  if(!u||!p){err.textContent='请输入用户名和密码';return}
  try{const d=await api('/login',{method:'POST',body:JSON.stringify({username:u,password:p})});token=d.token;currentUser=d.user;localStorage.setItem('ot_token',token);localStorage.setItem('ot_user',JSON.stringify(currentUser));enterApp()}catch(e){err.textContent=e.message}
};

OT.doRegister = async function doRegister(){
  const name=document.getElementById('reg-name').value.trim(),p=document.getElementById('reg-pass').value,p2=document.getElementById('reg-pass2').value,err=document.getElementById('reg-error');
  if(!name){err.textContent='请输入姓名';return}if(!p||p.length<4){err.textContent='密码至少4个字符';return}if(p!==p2){err.textContent='两次密码不一致';return}
  try{await api('/register',{method:'POST',body:JSON.stringify({displayName:name,password:p})});err.style.color='var(--success)';err.textContent='✅ 注册成功，请登录';setTimeout(()=>{err.style.color='';switchAuthTab('login')},2000)}catch(e){err.textContent=e.message}
};

OT.doLogout = function doLogout(){if(clockIntervalId){clearInterval(clockIntervalId);clockIntervalId=null}token=null;currentUser=null;localStorage.removeItem('ot_token');localStorage.removeItem('ot_user');document.getElementById('app-shell').style.display='none';document.getElementById('auth-screen').style.display='flex'};

OT.enterApp = async function enterApp(){
  document.getElementById('auth-screen').style.display='none';
  document.getElementById('app-shell').style.display='block';
  buildTabNav();
  applyRoleAccess();
  try{const d=await api('/settings');settings=d.settings||{}}catch(e){settings={}}
  if(currentUser.role==='admin'){showPage('dashboard');loadDashboard()}
  else{showPage('clock');startGeoWatch();updateClock();loadAllRecords().then(()=>{renderCalendar();renderStats();updateHeaderStats()});if(clockIntervalId)clearInterval(clockIntervalId);clockIntervalId=setInterval(updateClock,1000)}
  renderSettings();
};

OT.buildTabNav = function buildTabNav(){
  const nav=document.getElementById('tab-nav');
  if(currentUser.role==='admin'){
    nav.innerHTML=`
      <button class="tab-item active" data-page="dashboard" onclick="showPage('dashboard')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>概览</button>
      <button class="tab-item" data-page="members" onclick="showPage('members')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>成员</button>
      <button class="tab-item" data-page="email" onclick="showPage('email')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>邮件</button>
      <button class="tab-item" data-page="settings" onclick="showPage('settings')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>设置</button>`;
  } else {
    nav.innerHTML=`
      <button class="tab-item active" data-page="clock" onclick="showPage('clock')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12,6 12,12 16,14"/></svg>打卡</button>
      <button class="tab-item" data-page="records" onclick="showPage('records')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>记录</button>
      <button class="tab-item" data-page="stats" onclick="showPage('stats')"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>统计</button>`;
  }
};

OT.applyRoleAccess = function applyRoleAccess(){
  if(!currentUser)return;
  const isAdmin=currentUser.role==='admin';
  document.getElementById('user-badge').innerHTML=`<span class="role-dot ${isAdmin?'role-admin':'role-user'}"></span><span>${escapeHtml(currentUser.displayName)}（${isAdmin?'管理员':'成员'}）</span>`;
};
