// Statistics helpers and rendering
var OT = window.OT = window.OT || {};

OT.recordPairs = function recordPairs(recs){
  const pairs=[];let openIn=null;
  [...recs].sort((a,b)=>a.ts-b.ts).forEach(r=>{
    if(r.type==='in'){if(!openIn)openIn=r}
    else if(r.type==='out'&&openIn){pairs.push([openIn,r]);openIn=null}
  });
  return pairs;
};

OT.calcTodayOT = function calcTodayOT(recs,date){
  if(!recs.length)return 0;const wk=isWorkingDay(date);
  if(wk){let ms=0;recordPairs(recs).forEach(([i,o])=>{let d=Math.max(0,o.ts-i.ts);const im=msToMin(i.ts,i.time_str),om=msToMin(o.ts,o.time_str),ls=timeToMin(settings.lunchStart||'12:00'),le=timeToMin(settings.lunchEnd||'13:00');if(im<ls&&om>le)d-=(le-ls)*60000;ms+=Math.max(0,d)});return Math.max(0,Math.round((ms-getWorkMinutes()*60000)/60000))}
  else{return Math.round(recordPairs(recs).reduce((sum,[i,o])=>sum+Math.max(0,o.ts-i.ts),0)/60000)}
};

OT.isWorkingDay = function isWorkingDay(d){const dk=calendarKey(d);if((settings.holidays||[]).includes(dk))return false;if((settings.workdays||[]).includes(dk))return true;return(settings.weekdays||[1,2,3,4,5]).includes(d.getDay())};

OT.getWorkMinutes = function getWorkMinutes(){return timeToMin(settings.workEnd||'18:00')-timeToMin(settings.workStart||'09:00')-(timeToMin(settings.lunchEnd||'13:00')-timeToMin(settings.lunchStart||'12:00'))};

OT.renderStats = function renderStats(){
  const dg={};allRecords.forEach(r=>{if(!dg[r.date])dg[r.date]=[];dg[r.date].push(r)});
  let totalOT=0,wkOT=0,hdOT=0;
  Object.entries(dg).forEach(([date,recs])=>{const ot=calcTodayOT(recs,new Date(date+'T12:00:00'));totalOT+=ot;isWorkingDay(new Date(date+'T12:00:00'))?wkOT+=ot:hdOT+=ot});
  const days=Object.keys(dg).length;
  const tk=dateKey(new Date()),ws2=new Date(tk+'T12:00:00');ws2.setDate(ws2.getDate()-ws2.getDay()+1);const weekStart=calendarKey(ws2);
  let weekTotal=0,weekDays=0;
  Object.entries(dg).forEach(([date,recs])=>{const d=new Date(date+'T12:00:00');if(date>=weekStart&&date<=tk){const ot=calcTodayOT(recs,d);weekTotal+=ot;weekDays++}});
  document.getElementById('stats-content').innerHTML=`
    <div class="summary-row"><span class="label">本周工时</span><span class="value overtime">${formatMinutes(weekTotal)}（${weekDays}天）</span></div>
    <div class="summary-row"><span class="label">本月工时</span><span class="value overtime">${formatMinutes(totalOT)}（${days}天）</span></div>
    <div class="summary-row"><span class="label">工作日工时</span><span class="value">${formatMinutes(wkOT)}</span></div>
    <div class="summary-row"><span class="label">休息日工时</span><span class="value">${formatMinutes(hdOT)}</span></div>
    <div class="summary-row"><span class="label">日均工时</span><span class="value">${days?formatMinutes(Math.round(totalOT/days)):'0 分钟'}</span></div>`;
  // Monthly bar
  const mn={};allRecords.forEach(r=>{if(!r.date)return;const k=r.date.slice(0,7);if(!mn[k])mn[k]={s:new Set(),m:0};mn[k].s.add(r.date)});
  Object.keys(mn).forEach(k=>{let ot=0;const[y,m]=k.split('-').map(Number);Object.entries(dg).forEach(([date,recs])=>{const d=new Date(date+'T12:00:00');if(d.getFullYear()===y&&d.getMonth()===m-1)ot+=calcTodayOT(recs,d)});mn[k].m=ot});
  const sm=Object.keys(mn).sort().reverse().slice(0,6);
  if(sm.length){const mx=Math.max(...sm.map(k=>mn[k].m),1);document.getElementById('stats-bar-chart').innerHTML=sm.map(m=>`<div style="margin-bottom:12px"><div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:4px"><span>${m}（${mn[m].s.size}天）</span><span style="font-weight:600;color:var(--accent)">${formatMinutes(mn[m].m)}</span></div><div style="height:20px;background:#F1F5F9;border-radius:4px;overflow:hidden"><div style="height:100%;width:${(mn[m].m/mx*100).toFixed(1)}%;background:linear-gradient(90deg,var(--primary),var(--primary-light));border-radius:4px"></div></div></div>`).join('')}
  else document.getElementById('stats-bar-chart').innerHTML='<div class="empty-state"><div class="text">暂无数据</div></div>';
};

OT.updateHeaderStats = function updateHeaderStats(){
  if(currentUser.role==='admin')return;
  const now=new Date(),tk=dateKey(now);
  const tr=allRecords.filter(r=>r.date===tk),tot=calcTodayOT(tr,new Date(tk+'T12:00:00'));
  const thisMonth=tk.slice(0,7);
  const mr=allRecords.filter(r=>r.date&&r.date.startsWith(thisMonth));
  const mdg={};mr.forEach(r=>{if(!mdg[r.date])mdg[r.date]=[];mdg[r.date].push(r)});
  let mot=0;Object.entries(mdg).forEach(([date,recs])=>{mot+=calcTodayOT(recs,new Date(date+'T12:00:00'))});
  const adg={};allRecords.forEach(r=>{if(!adg[r.date])adg[r.date]=[];adg[r.date].push(r)});
  let ttot=0;Object.entries(adg).forEach(([date,recs])=>{ttot+=calcTodayOT(recs,new Date(date+'T12:00:00'))});
  const todayObj=new Date(tk+'T12:00:00');todayObj.setDate(todayObj.getDate()-todayObj.getDay()+1);
  const weekStart=calendarKey(todayObj);
  const wRecs=allRecords.filter(r=>r.date>=weekStart&&r.date<=tk);
  const wdg={};wRecs.forEach(r=>{if(!wdg[r.date])wdg[r.date]=[];wdg[r.date].push(r)});
  let wot=0;Object.entries(wdg).forEach(([date,recs])=>{wot+=calcTodayOT(recs,new Date(date+'T12:00:00'))});
  document.getElementById('header-stats').innerHTML=`
    <div class="stat-item"><div class="stat-value">${fmtMin(tot)}</div><div class="stat-label">今日</div></div>
    <div class="stat-item"><div class="stat-value">${fmtMin(wot)}</div><div class="stat-label">本周</div></div>
    <div class="stat-item"><div class="stat-value">${fmtMin(mot)}</div><div class="stat-label">本月</div></div>`;
};
