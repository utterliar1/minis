// Shared state and utilities
var OT = window.OT = window.OT || {};

OT.API_BASE = location.origin;
OT.token = localStorage.getItem('ot_token') || null;
OT.currentUser = JSON.parse(localStorage.getItem('ot_user') || 'null');
OT.settings = {};
OT.allRecords = [];
OT.currentPos = null;
OT.calMonth = new Date().getMonth();
OT.calYear = new Date().getFullYear();
OT.clockIntervalId = null;

Object.defineProperties(window, Object.fromEntries([
  'API_BASE','token','currentUser','settings','allRecords','currentPos','calMonth','calYear','clockIntervalId'
].map((key) => [key, {
  configurable: true,
  get(){ return OT[key]; },
  set(value){ OT[key] = value; }
}])));

OT.escapeHtml = function escapeHtml(v){return String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))};

OT.safeToken = function safeToken(v){return encodeURIComponent(String(v??'')).replace(/'/g,'%27')};

OT.csvCell = function csvCell(v){return `"${String(v??'').replace(/"/g,'""')}"`};

OT.api = async function api(path, opts={}) {
  const headers = {'Content-Type':'application/json', ...(opts.headers||{})};
  if (token) headers['Authorization'] = 'Bearer ' + token;
  const resp = await fetch(API_BASE+'/api'+path, {...opts, headers});
  const data = await resp.json();
  if (!resp.ok) throw new Error(data.error||'请求失败');
  return data;
};

OT.bjNow = function bjNow(){return new Date(Date.now()+8*3600000)};

OT.bjTimeStr = function bjTimeStr(d){return d.toISOString().slice(11,19)};

OT.bjDateStr = function bjDateStr(d){return d.toISOString().slice(0,10)};

OT.bjWeekday = function bjWeekday(d){return['周日','周一','周二','周三','周四','周五','周六'][d.getUTCDay()]};

OT.dateKey = function dateKey(d){const x=d?new Date(d.getTime()+8*3600000):bjNow();return x.getUTCFullYear()+'-'+String(x.getUTCMonth()+1).padStart(2,'0')+'-'+String(x.getUTCDate()).padStart(2,'0')};

OT.calendarKey = function calendarKey(d){return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')};

OT.timeToMin = function timeToMin(t){if(!t)return 540;const[h,m]=t.split(':');return +h*60+(+m)};

OT.msToMin = function msToMin(ts,timeStr){if(timeStr){const p=timeStr.split(':');return(+p[0])*60+(+p[1])}const d=new Date(ts+8*3600000);return d.getUTCHours()*60+d.getUTCMinutes()};

OT.formatMinutes = function formatMinutes(m){if(m<=0)return'0 分钟';const h=Math.floor(m/60),mm=m%60;return h===0?`${mm} 分钟`:mm===0?`${h} 小时`:`${h} 小时 ${mm} 分钟`};

OT.fmtMin = function fmtMin(m){if(m<=0)return'0h';const h=Math.floor(m/60),mm=m%60;return mm===0?`${h}h`:`${h}h${mm}m`};

OT.downloadBlob = function downloadBlob(b,n){const u=URL.createObjectURL(b);const a=document.createElement('a');a.href=u;a.download=n;a.click();URL.revokeObjectURL(u)};

OT.showToast = function showToast(msg){const t=document.getElementById('toast');t.textContent=msg;t.classList.add('show');setTimeout(()=>t.classList.remove('show'),2000)};

OT.showModal = function showModal(html){document.getElementById('modal-body').innerHTML=html;document.getElementById('modal').classList.add('show')};

OT.closeModal = function closeModal(e){if(e.target===document.getElementById('modal'))document.getElementById('modal').classList.remove('show')};

OT.closeModalDirect = function closeModalDirect(){document.getElementById('modal').classList.remove('show')};

OT.showConfirmModal = function showConfirmModal(title,msg,onOk){showModal(`<div class="modal-title">${escapeHtml(title)}</div><p style="text-align:center;color:var(--text-sec);margin-bottom:16px;white-space:pre-line">${escapeHtml(msg)}</p><div class="btn-group"><button class="btn btn-outline" onclick="closeModalDirect()">取消</button><button class="btn btn-primary" id="modal-ok-btn">确认</button></div>`);document.getElementById('modal-ok-btn').onclick=()=>{closeModalDirect();onOk()}};
