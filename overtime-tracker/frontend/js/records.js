// Records calendar and export
var OT = window.OT = window.OT || {};

OT.loadAllRecords = async function loadAllRecords(){
  recordsLoaded=false;
  try{
    const d=await api('/records');
    allRecords=d.records||[];
    recordsLoaded=true;
  }catch(e){
    allRecords=[];
    recordsLoaded=false;
    throw e;
  }
};

OT.renderCalendar = function renderCalendar(){
  const grid=document.getElementById('calendar-grid'),label=document.getElementById('cal-month-label');
  label.textContent=`${calYear}年${calMonth+1}月`;
  const fd=new Date(calYear,calMonth,1).getDay(),dim=new Date(calYear,calMonth+1,0).getDate(),today=new Date();
  let html=['日','一','二','三','四','五','六'].map(d=>`<div class="cal-header">${d}</div>`).join('');
  const rd=new Set();allRecords.forEach(r=>{if(!r.date)return;const [y,m,d]=r.date.split('-').map(Number);if(m-1===calMonth&&y===calYear)rd.add(d)});
  for(let i=0;i<fd;i++)html+=`<div class="cal-day" style="visibility:hidden"></div>`;
  for(let d=1;d<=dim;d++){const it=d===today.getDate()&&calMonth===today.getMonth()&&calYear===today.getFullYear();html+=`<div class="cal-day ${it?'today':''} ${rd.has(d)?'has-record':''}" onclick="event.stopPropagation();showDayDetail(${calYear},${calMonth},${d})">${d}</div>`}
  grid.innerHTML=html;
  // Month summary
  const dg={};allRecords.forEach(r=>{if(!r.date)return;const [y,m]=r.date.split('-').map(Number);if(m-1===calMonth&&y===calYear){if(!dg[r.date])dg[r.date]=[];dg[r.date].push(r)}});
  let ot=0;Object.entries(dg).forEach(([date,recs])=>{ot+=calcTodayOT(recs,new Date(date+'T12:00:00'))});
  document.getElementById('summary-month-info').textContent=`${Object.keys(dg).length} 天打卡，工时 ${formatMinutes(ot)}`;
  renderRecordsList(dg);
};

OT.changeMonth = function changeMonth(d){calMonth+=d;if(calMonth>11){calMonth=0;calYear++}if(calMonth<0){calMonth=11;calYear--}renderCalendar()};

OT.renderRecordsList = function renderRecordsList(dg){
  const c=document.getElementById('records-list'),sorted=Object.keys(dg).sort().reverse();
  if(!sorted.length){c.innerHTML='<div class="empty-state"><div class="icon">📭</div><div class="text">暂无记录</div></div>';return}
  c.innerHTML=sorted.map(date=>{
    const d=new Date(date+'T12:00:00'),recs=dg[date].sort((a,b)=>a.ts-b.ts),ot=calcTodayOT(recs,d),wd=['周日','周一','周二','周三','周四','周五','周六'][d.getDay()];
    const fi=recs.find(r=>r.type==='in'),lo=[...recs].reverse().find(r=>r.type==='out');
    const badge=isWorkingDay(d)?'<span class="badge badge-workday">工作日</span>':'<span class="badge badge-holiday">休息日</span>';
    const rangeBadge=recs.some(r=>Number(r.out_of_range)===1)?'<span class="record-flag-badge">范围外</span>':'';
    return`<div class="record-card"><div><div><span class="record-date">${d.getMonth()+1}月${d.getDate()}日</span><span class="record-weekday">${wd}</span></div><div class="record-times">${fi?(fi.time_str||'').slice(0,5):'--:--'} → ${lo?(lo.time_str||'').slice(0,5):'进行中'}</div><div class="record-badges">${badge}${rangeBadge}</div></div><div><div class="record-overtime">+${formatMinutes(ot)}</div></div></div>`;
  }).join('');
};

OT.exportMyRecords = async function exportMyRecords(){
  try{await OT.refreshSettings();const d=await api('/records');const recs=d.records||[];if(!recs.length){showToast('暂无数据');return}
    const dn=(currentUser&&currentUser.displayName)||'';
    const csv=OT.buildExportCsv(recs.map(r=>({...r,display_name:r.display_name||dn||r.user_id})));
    downloadBlob(new Blob(['\uFEFF'+csv],{type:'text/csv;charset=utf-8'}),`我的工时记录_${dateKey(new Date())}.csv`);showToast('📤 已导出');
  }catch(e){showToast('导出失败')}
};

OT.showDayDetail = function showDayDetail(y,m,d){
  const dateStr=`${y}-${String(m+1).padStart(2,'0')}-${String(d).padStart(2,'0')}`;
  const dayRecs=allRecords.filter(r=>r.date===dateStr).sort((a,b)=>a.ts-b.ts);
  const dt=new Date(dateStr+'T12:00:00');
  const wd=['周日','周一','周二','周三','周四','周五','周六'][dt.getDay()];
  const isWk=isWorkingDay(dt),ot=calcTodayOT(dayRecs,dt);
  const badge=isWk?'工作日':'休息日';
  let c=`<div class="modal-title">${m+1}月${d}日 ${wd}（${badge}）</div>`;
  if(!dayRecs.length){c+=`<div style="text-align:center;padding:20px;color:var(--text-sec)">当天无打卡记录</div>`}
  else{
    c+=`<div style="margin-bottom:12px">`;
    dayRecs.forEach(r=>{const icon=r.type==='in'?'🟢':'🔴',label=r.type==='in'?'上班':'下班';
      c+=`<div style="display:flex;align-items:center;gap:8px;padding:8px 0;border-bottom:1px solid var(--border)"><span>${icon}</span><div style="flex:1"><div style="font-weight:600;font-size:15px">${label} ${escapeHtml(r.time_str||'')}</div>${OT.actualLocationHtml(r)}${r.note?`<div style="font-size:12px;color:var(--text-sec);margin-top:2px">${escapeHtml(r.note)}</div>`:''}</div></div>`});
    c+=`</div>`;
    c+=`<div style="background:#EEF2FF;border-radius:8px;padding:12px;text-align:center;font-weight:600;color:var(--primary)">工时：${formatMinutes(ot)}</div>`;
  }
  c+=`<button class="btn btn-primary btn-block" style="margin-top:12px" onclick="closeModalDirect()">关闭</button>`;
  showModal(c);
};
