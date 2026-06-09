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

OT.csvHourText = function csvHourText(minutes){
  const m=Math.max(0,Number(minutes)||0),h=Math.floor(m/60),mm=m%60;
  if(h<=0)return `${mm}m`;
  return mm?`${h}h${mm}m`:`${h}h`;
};

OT.groupExportRecords = function groupExportRecords(records){
  const groups={};
  (records||[]).forEach(r=>{
    const key=(r.user_id||r.display_name||'')+'|'+r.date;
    if(!groups[key])groups[key]={name:r.display_name||r.user_id||'',personKey:r.user_id||r.display_name||'',date:r.date,records:[]};
    groups[key].records.push(r);
  });
  return Object.values(groups).sort((a,b)=>a.date===b.date?String(a.name).localeCompare(String(b.name)):String(a.date).localeCompare(String(b.date)));
};

OT.exportRowFromGroup = function exportRowFromGroup(group){
  const d=new Date(group.date+'T12:00:00');
  const wd=['周日','周一','周二','周三','周四','周五','周六'][d.getDay()];
  const sorted=[...group.records].sort((a,b)=>a.ts-b.ts);
  const fi=sorted.find(r=>r.type==='in');
  const lo=[...sorted].reverse().find(r=>r.type==='out');
  const reasons=sorted.filter(r=>r.note).map(r=>r.note).filter(Boolean).join('; ');
  const remote=sorted.some(r=>Number(r.out_of_range)===1);
  const minutes=OT.calcTodayOT(sorted,d);
  return {
    name: group.name,
    personKey: group.personKey,
    date: group.date,
    weekday: wd,
    firstIn: fi?(fi.time_str||'').slice(0,5):'',
    lastOut: lo?(lo.time_str||'').slice(0,5):'',
    type: OT.isWorkingDay(d)?'工作日':'休息日',
    reasons,
    remoteText: remote?'是':'',
    remote,
    minutes,
    hours: OT.csvHourText(minutes)
  };
};

OT.exportDetailLine = function exportDetailLine(r){
  return [
    OT.csvCell(r.name),OT.csvCell(r.date),OT.csvCell(r.weekday),OT.csvCell(r.firstIn),OT.csvCell(r.lastOut),
    OT.csvCell(r.type),OT.csvCell(r.reasons),OT.csvCell(r.remoteText),r.minutes,OT.csvCell(r.hours)
  ].join(',');
};

OT.exportSummaryLine = function exportSummaryLine(label, rows){
  const totalMinutes=rows.reduce((sum,r)=>sum+r.minutes,0);
  const remoteDays=rows.filter(r=>r.remote).length;
  return [OT.csvCell(label),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(`远程天数 ${remoteDays}`),totalMinutes,OT.csvCell(OT.csvHourText(totalMinutes))].join(',');
};

OT.buildExportCsv = function buildExportCsv(records, options={}){
  const rows=OT.groupExportRecords(records).map(OT.exportRowFromGroup);
  const lines=[];
  if(options&&options.includePersonSubtotals){
    const people={};
    rows.forEach(r=>{
      const key=r.personKey||r.name||'';
      if(!people[key])people[key]={name:r.name||key,rows:[]};
      people[key].rows.push(r);
    });
    Object.values(people)
      .sort((a,b)=>String(a.name).localeCompare(String(b.name)))
      .forEach(person=>{
        person.rows
          .sort((a,b)=>String(a.date).localeCompare(String(b.date)))
          .forEach(r=>lines.push(OT.exportDetailLine(r)));
        lines.push(OT.exportSummaryLine(`${person.name} 小计`,person.rows));
      });
  }else{
    rows.forEach(r=>lines.push(OT.exportDetailLine(r)));
  }
  lines.push(OT.exportSummaryLine('汇总',rows));
  return '姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)\n'+lines.join('\n');
};

OT.geoErrorMessage = function geoErrorMessage(err){
  const code=err&&Number(err.code);
  const msg=String(err&&err.message||'').toLowerCase();
  if(code===1||msg.includes('denied')||msg.includes('permission'))return '定位权限未开启，请在浏览器地址栏允许位置访问后重试';
  if(msg.includes('secure origin'))return '当前页面不支持定位，请使用 localhost、HTTPS 或受信任的访问地址';
  if(code===2)return '暂时无法获取当前位置，请检查网络或系统定位服务';
  if(code===3||msg.includes('timeout'))return '获取位置超时，请移到信号更好的位置后重试';
  return '获取位置失败，请检查定位权限和网络后重试';
};

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

OT.timeToMin = function timeToMin(t){if(!t)return 510;const[h,m]=t.split(':');return +h*60+(+m)};

OT.msToMin = function msToMin(ts,timeStr){if(timeStr){const p=timeStr.split(':');return(+p[0])*60+(+p[1])}const d=new Date(ts+8*3600000);return d.getUTCHours()*60+d.getUTCMinutes()};

OT.formatMinutes = function formatMinutes(m){if(m<=0)return'0 分钟';const h=Math.floor(m/60),mm=m%60;return h===0?`${mm} 分钟`:mm===0?`${h} 小时`:`${h} 小时 ${mm} 分钟`};

OT.fmtMin = function fmtMin(m){if(m<=0)return'0h';const h=Math.floor(m/60),mm=m%60;return mm===0?`${h}h`:`${h}h${mm}m`};

OT.downloadBlob = function downloadBlob(b,n){const u=URL.createObjectURL(b);const a=document.createElement('a');a.href=u;a.download=n;a.click();URL.revokeObjectURL(u)};

OT.showToast = function showToast(msg){const t=document.getElementById('toast');t.textContent=msg;t.classList.add('show');setTimeout(()=>t.classList.remove('show'),2000)};

OT.showModal = function showModal(html){document.getElementById('modal-body').innerHTML=html;document.getElementById('modal').classList.add('show')};

OT.closeModal = function closeModal(e){if(e.target===document.getElementById('modal'))document.getElementById('modal').classList.remove('show')};

OT.closeModalDirect = function closeModalDirect(){document.getElementById('modal').classList.remove('show')};

OT.showConfirmModal = function showConfirmModal(title,msg,onOk){showModal(`<div class="modal-title">${escapeHtml(title)}</div><p style="text-align:center;color:var(--text-sec);margin-bottom:16px;white-space:pre-line">${escapeHtml(msg)}</p><div class="btn-group"><button class="btn btn-outline" onclick="closeModalDirect()">取消</button><button class="btn btn-primary" id="modal-ok-btn">确认</button></div>`);document.getElementById('modal-ok-btn').onclick=()=>{closeModalDirect();onOk()}};
