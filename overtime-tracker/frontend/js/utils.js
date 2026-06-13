// Shared state and utilities
var OT = window.OT = window.OT || {};

OT.API_BASE = location.origin;
OT.token = localStorage.getItem('ot_token') || null;
OT.currentUser = JSON.parse(localStorage.getItem('ot_user') || 'null');
OT.settings = {};
OT.allRecords = [];
OT.recordsLoaded = false;
OT.currentPos = null;
OT.calMonth = new Date().getMonth();
OT.calYear = new Date().getFullYear();
OT.clockIntervalId = null;

Object.defineProperties(window, Object.fromEntries([
  'API_BASE','token','currentUser','settings','allRecords','recordsLoaded','currentPos','calMonth','calYear','clockIntervalId'
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

OT.haversineDistance = OT.haversineDistance || function haversineDistance(a,b,c,d){const R=6371000,e=(c-a)*Math.PI/180,f=(d-b)*Math.PI/180,g=Math.sin(e/2)**2+Math.cos(a*Math.PI/180)*Math.cos(c*Math.PI/180)*Math.sin(f/2)**2;return R*2*Math.atan2(Math.sqrt(g),Math.sqrt(1-g))};

OT.actualLocationText = function actualLocationText(record){
  if(!record||Number(record.out_of_range)!==1)return '';
  const hasLat=record.lat!==null&&record.lat!==undefined&&record.lat!=='',hasLng=record.lng!==null&&record.lng!==undefined&&record.lng!=='';
  const lat=Number(record.lat),lng=Number(record.lng),acc=Number(record.accuracy);
  const address=record.actual_address||record.location_address||record.address||'';
  const cfg=OT.settings||settings||{};
  const hasMapKey=['mapKey','mapApiKey','amapKey','gaodeMapKey','locationMapKey'].some(k=>String(cfg[k]||'').trim());
  const parts=[];
  if(hasLat&&hasLng&&Number.isFinite(lat)&&Number.isFinite(lng)){
    parts.push(`${lat.toFixed(6)},${lng.toFixed(6)}`);
    if(Number.isFinite(acc))parts.push(`精度 ${Math.round(acc)}m`);
    const hasBase=cfg&&cfg.lat!==null&&cfg.lat!==undefined&&cfg.lat!==''&&cfg.lng!==null&&cfg.lng!==undefined&&cfg.lng!=='';
    const baseLat=Number(cfg&&cfg.lat),baseLng=Number(cfg&&cfg.lng);
    if(hasBase&&Number.isFinite(baseLat)&&Number.isFinite(baseLng))parts.push(`距离 ${Math.round(OT.haversineDistance(baseLat,baseLng,lat,lng))}m`);
  }
  if(address&&hasMapKey)parts.push(`地址 ${address}`);
  return parts.join('; ');
};

OT.actualLocationHtml = function actualLocationHtml(record){
  const text=OT.actualLocationText(record);
  return text?`<div class="note-text">实际位置：${OT.escapeHtml(text)}</div>`:'';
};

OT.groupExportRecords = function groupExportRecords(records){
  const groups={},openByPerson={};
  (records||[]).slice().sort((a,b)=>a.ts-b.ts).forEach(r=>{
    const personKey=r.user_id||r.display_name||'';
    if(r.type==='in'){
      openByPerson[personKey]=r;
      const key=personKey+'|'+r.date;
      if(!groups[key])groups[key]={name:r.display_name||r.user_id||'',personKey,date:r.date,records:[]};
      groups[key].records.push(r);
      return;
    }
    if(r.type==='out'&&openByPerson[personKey]){
      const start=openByPerson[personKey];
      const key=personKey+'|'+start.date;
      if(!groups[key])groups[key]={name:start.display_name||r.display_name||start.user_id||personKey,personKey,date:start.date,records:[]};
      groups[key].records.push(r);
      openByPerson[personKey]=null;
      return;
    }
    const key=personKey+'|'+r.date;
    if(!groups[key])groups[key]={name:r.display_name||r.user_id||'',personKey,date:r.date,records:[]};
    groups[key].records.push(r);
  });
  return Object.values(groups).sort((a,b)=>a.date===b.date?String(a.name).localeCompare(String(b.name)):String(a.date).localeCompare(String(b.date)));
};

OT.groupRecordsByStartDate = function groupRecordsByStartDate(records){
  const groups={},openByPerson={};
  (records||[]).slice().sort((a,b)=>a.ts-b.ts).forEach(r=>{
    const personKey=r.user_id||r.display_name||'';
    if(r.type==='in'){
      openByPerson[personKey]=r;
      if(!groups[r.date])groups[r.date]=[];
      groups[r.date].push(r);
      return;
    }
    if(r.type==='out'&&openByPerson[personKey]){
      const start=openByPerson[personKey];
      if(!groups[start.date])groups[start.date]=[];
      groups[start.date].push(r);
      openByPerson[personKey]=null;
      return;
    }
    if(!groups[r.date])groups[r.date]=[];
    groups[r.date].push(r);
  });
  return groups;
};

OT.splitWorkNote = function splitWorkNote(note){
  const text=String(note||'').trim();
  const idx=text.search(/[：:]/);
  if(idx<=0)return {category:'',reason:text};
  return {category:text.slice(0,idx).trim(),reason:text.slice(idx+1).trim()};
};

OT.exportReviewFlags = function exportReviewFlags(records){
  const flags=[],pairs=OT.recordPairs(records||[]);
  pairs.forEach(([i,o])=>{
    if(Number(i.out_of_range||0)!==Number(o.out_of_range||0)&&!flags.includes('位置不一致'))flags.push('位置不一致');
    if(i.date&&o.date&&i.date!==o.date&&!flags.includes('跨天'))flags.push('跨天');
  });
  return flags;
};

OT.exportRowFromGroup = function exportRowFromGroup(group){
  const d=new Date(group.date+'T12:00:00');
  const wd=['周日','周一','周二','周三','周四','周五','周六'][d.getDay()];
  const sorted=[...group.records].sort((a,b)=>a.ts-b.ts);
  const fi=sorted.find(r=>r.type==='in');
  const lo=[...sorted].reverse().find(r=>r.type==='out');
  const inNotes=sorted.filter(r=>r.type==='in'&&r.note).map(r=>OT.splitWorkNote(r.note));
  const categories=[...new Set(inNotes.map(n=>n.category).filter(Boolean))].join('; ');
  const reasons=inNotes.map(n=>n.reason).filter(Boolean).join('; ');
  const clockOutNotes=sorted.filter(r=>r.type==='out'&&r.note).map(r=>r.note).filter(Boolean).join('; ');
  const remote=sorted.some(r=>Number(r.out_of_range)===1);
  const actualLocation=sorted.map(OT.actualLocationText).find(Boolean)||'';
  const minutes=OT.calcTodayOT(sorted,d);
  const reviewFlags=OT.exportReviewFlags(sorted).join('；');
  return {
    name: group.name,
    personKey: group.personKey,
    date: group.date,
    weekday: wd,
    firstIn: fi?(fi.time_str||'').slice(0,5):'',
    lastOut: lo?(lo.time_str||'').slice(0,5):'',
    type: OT.isWorkingDay(d)?'工作日':'休息日',
    categories,
    reasons,
    clockOutNotes,
    remoteText: remote?'是':'',
    actualLocation,
    reviewFlags,
    remote,
    minutes,
    hours: OT.csvHourText(minutes)
  };
};

OT.exportDetailLine = function exportDetailLine(r){
  return [
    OT.csvCell(r.name),OT.csvCell(r.date),OT.csvCell(r.weekday),OT.csvCell(r.firstIn),OT.csvCell(r.lastOut),
    OT.csvCell(r.type),OT.csvCell(r.categories),OT.csvCell(r.reasons),OT.csvCell(r.clockOutNotes),
    OT.csvCell(r.remoteText),OT.csvCell(r.actualLocation),OT.csvCell(r.reviewFlags),r.minutes,OT.csvCell(r.hours)
  ].join(',');
};

OT.exportSummaryLine = function exportSummaryLine(label, rows, typeLabel){
  const totalMinutes=rows.reduce((sum,r)=>sum+r.minutes,0);
  const remoteDays=rows.filter(r=>r.remote).length;
  return [
    OT.csvCell(label),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(typeLabel||''),
    OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(`范围外 ${remoteDays} 天`),OT.csvCell(''),OT.csvCell(''),
    totalMinutes,OT.csvCell(OT.csvHourText(totalMinutes))
  ].join(',');
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
        lines.push(OT.exportSummaryLine(person.name,person.rows,'小计'));
      });
  }else{
    rows.forEach(r=>lines.push(OT.exportDetailLine(r)));
  }
  lines.push(OT.exportSummaryLine('汇总',rows,'总计'));
  return '姓名,日期,星期,上班,下班,类型,工作类别,事由,下班说明,范围外,实际位置,复核标记,工时(分),工时(h)\n'+lines.join('\n');
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

OT.refreshSettings = async function refreshSettings(){
  try{
    const d=await api('/settings');
    if(d&&d.settings)settings=d.settings;
  }catch(e){}
  return settings;
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
