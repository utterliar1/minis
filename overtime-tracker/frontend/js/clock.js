// Clocking and geolocation
var OT = window.OT = window.OT || {};

OT.updateClock = function updateClock(){
  const now=bjNow();
  document.getElementById('clock-time').textContent=bjTimeStr(now);
  document.getElementById('clock-date').textContent=`${now.getUTCFullYear()}年${now.getUTCMonth()+1}月${now.getUTCDate()}日 ${bjWeekday(now)}`;
  updateClockButton();updateTodayTimeline();updateHeaderStats();
};

OT.isWithinRange = function isWithinRange(pos){if(settings.lat==null||settings.lng==null||!pos)return false;return haversineDistance(settings.lat,settings.lng,pos.lat,pos.lng)<=settings.radius};

OT.haversineDistance = OT.haversineDistance || function haversineDistance(a,b,c,d){const R=6371000,e=(c-a)*Math.PI/180,f=(d-b)*Math.PI/180,g=Math.sin(e/2)**2+Math.cos(a*Math.PI/180)*Math.cos(c*Math.PI/180)*Math.sin(f/2)**2;return R*2*Math.atan2(Math.sqrt(g),Math.sqrt(1-g))};

OT.startGeoWatch = function startGeoWatch(){
  if(!navigator.geolocation){document.getElementById('clock-status').innerHTML='⚠️ 不支持定位';return}
  navigator.geolocation.watchPosition(p=>{OT.lastGeoErrorMessage='';currentPos={lat:p.coords.latitude,lng:p.coords.longitude,accuracy:p.coords.accuracy};updateGeoStatus();updateClockButton()},e=>{OT.lastGeoErrorMessage=OT.geoErrorMessage(e);currentPos=null;document.getElementById('clock-status').innerHTML='⚠️ '+OT.escapeHtml(OT.lastGeoErrorMessage);updateClockButton()},{enableHighAccuracy:true,maximumAge:10000,timeout:15000});
};

OT.updateGeoStatus = function updateGeoStatus(){
  if(!currentPos)return;const el=document.getElementById('clock-status');
  if(settings.lat==null||settings.lng==null){el.innerHTML='⚠️ 未设置打卡地点';return}
  const dist=Math.round(haversineDistance(settings.lat,settings.lng,currentPos.lat,currentPos.lng)),acc=Math.round(currentPos.accuracy);
  if(acc>(settings.gpsAccuracy||100))el.innerHTML=`📍 GPS精度: ${acc}m，不足`;
  else if(isWithinRange(currentPos))el.innerHTML=`<span class="in-range">✅ 范围内</span>（${dist}m）`;
  else el.innerHTML=`<span class="out-range">❌ 范围外</span>（${dist}m）`;
};

OT.updateClockButton = function updateClockButton(){
  const btn=document.getElementById('clock-btn'),txt=document.getElementById('clock-btn-text'),last=OT.getLastClockRecord();
  if((settings.lat==null||settings.lng==null)&&!currentPos){btn.className='clock-btn disabled';txt.textContent='请等待管理员配置';return}
  if(!currentPos){btn.className='clock-btn disabled';txt.textContent=OT.lastGeoErrorMessage?'定位未开启':'获取位置中';return}
  if(currentPos&&currentPos.accuracy>(settings.gpsAccuracy||100)){btn.className='clock-btn disabled';txt.textContent='GPS精度不足';return}
  if(!last){btn.className='clock-btn check-in';txt.textContent='打卡上班'}
  else if(last.type==='in'){btn.className='clock-btn check-out';txt.textContent='打卡下班'}
  else{btn.className='clock-btn check-in';txt.textContent='再次打卡'}
};

OT.getLastTodayRecord = function getLastTodayRecord(){const t=dateKey(new Date());const r=allRecords.filter(x=>x.date===t).sort((a,b)=>a.ts-b.ts);return r.length?r[r.length-1]:null};

OT.getLastClockRecord = function getLastClockRecord(){
  const recs=(allRecords||[]).filter(r=>!currentUser||!r.user_id||r.user_id===currentUser.username).sort((a,b)=>a.ts-b.ts);
  return recs.length?recs[recs.length-1]:null;
};

OT.showLastClockResult = function showLastClockResult(record,durationText){
  const el=document.getElementById('last-clock-result');
  if(!el)return;
  const label=record.type==='in'?'上班记录':'下班记录';
  el.style.display='block';
  el.innerHTML=`<div class="result-main">${label} ${OT.escapeHtml(record.time_str||'')}</div><div class="result-sub">${durationText?`本次工时 ${OT.escapeHtml(durationText)}<br>`:''}${record.out_of_range?'范围外已标记<br>':''}${OT.actualLocationHtml(record)}${OT.escapeHtml(record.note||'')}</div>`;
};

OT.getCurrentClockIn = function getCurrentClockIn(records,outRecord){
  let openIn=null,lastIn=null;
  records.filter(r=>r.ts<=outRecord.ts).sort((a,b)=>a.ts-b.ts).forEach(r=>{
    if(r===outRecord)return;
    if(r.type==='in'){openIn=r;lastIn=r}
    else if(r.type==='out'&&openIn){openIn=null}
  });
  return openIn||lastIn;
};

OT.WORK_CATEGORIES = ['设计','销售','采购','招投标','项目管理','现场支持','售后/客户沟通','内部事务','其他'];

OT.clockOutReviewContext = function clockOutReviewContext(clockIn,outOfRange,dateStr){
  const flags=[];
  if(clockIn&&Number(clockIn.out_of_range||0)!==Number(outOfRange?1:0))flags.push('位置不一致');
  if(clockIn&&clockIn.date&&dateStr&&clockIn.date!==dateStr)flags.push('跨天');
  const crossDay=flags.includes('跨天');
  const options=crossDay
    ? ['实际工作持续到次日','忘记下班打卡，当前补记','定位或设备原因导致延后记录','跨天且结束位置变化','其他']
    : ['临时外出后结束记录','返回办公地点后结束记录','定位精度异常','其他'];
  return {required:flags.length>0,flags,options};
};

OT.showClockInNoteModal = function showClockInNoteModal(){
  return new Promise(resolve=>{
    const options=OT.WORK_CATEGORIES.map(v=>`<option value="${OT.escapeHtml(v)}">${OT.escapeHtml(v)}</option>`).join('');
    showModal(`<div class="modal-title">填写上班信息</div>
      <div class="export-modal-field"><label class="export-modal-label">工作类别</label><select id="clock-in-category" class="export-modal-select">${options}</select></div>
      <div class="export-modal-field"><label class="export-modal-label">事由</label><textarea id="clock-in-reason" class="export-modal-select" rows="3" placeholder="请填写具体事由"></textarea></div>
      <div class="btn-group"><button class="btn btn-outline" id="clock-in-cancel">取消</button><button class="btn btn-primary" id="clock-in-ok">确认</button></div>`);
    document.getElementById('clock-in-cancel').onclick=()=>{closeModalDirect();resolve(null)};
    document.getElementById('clock-in-ok').onclick=()=>{
      const category=(document.getElementById('clock-in-category').value||'').trim();
      const reason=(document.getElementById('clock-in-reason').value||'').trim();
      if(!reason){showToast('请输入事由');return}
      closeModalDirect();resolve(`${category}：${reason}`);
    };
  });
};

OT.showClockOutNoteModal = function showClockOutNoteModal(context){
  return new Promise(resolve=>{
    const options=(context.options||[]).map(v=>`<option value="${OT.escapeHtml(v)}">${OT.escapeHtml(v)}</option>`).join('');
    const flagText=(context.flags||[]).join('；');
    showModal(`<div class="modal-title">填写下班说明</div>
      <p style="text-align:center;color:var(--text-sec);margin-bottom:12px">该记录需要复核：${OT.escapeHtml(flagText)}</p>
      <div class="export-modal-field"><label class="export-modal-label">快捷说明</label><select id="clock-out-reason-option" class="export-modal-select">${options}</select></div>
      <div class="export-modal-field"><label class="export-modal-label">补充说明</label><textarea id="clock-out-reason-extra" class="export-modal-select" rows="3" placeholder="选择其他时请填写；也可补充实际结束情况"></textarea></div>
      <div class="btn-group"><button class="btn btn-outline" id="clock-out-cancel">取消</button><button class="btn btn-primary" id="clock-out-ok">确认</button></div>`);
    document.getElementById('clock-out-cancel').onclick=()=>{closeModalDirect();resolve(null)};
    document.getElementById('clock-out-ok').onclick=()=>{
      const selected=(document.getElementById('clock-out-reason-option').value||'').trim();
      const extra=(document.getElementById('clock-out-reason-extra').value||'').trim();
      if(selected==='其他'&&!extra){showToast('请填写下班说明');return}
      closeModalDirect();resolve(extra&&selected!=='其他'?`${selected}：${extra}`:(extra||selected));
    };
  });
};

OT.handleClock = async function handleClock(){
  const btn=document.getElementById('clock-btn');
  if(btn.classList.contains('disabled')){showToast('请先满足打卡条件');return}
  if(!currentPos){showToast(OT.lastGeoErrorMessage||'正在获取位置...');return}
  if(settings.lat==null||settings.lng==null){showToast('打卡地点未配置');return}
  const last=OT.getLastClockRecord();let type='in';
  if(last&&last.type==='in')type='out';
  let reason='';
  if(type==='in'){
    reason=await OT.showClockInNoteModal();
    if(reason===null)return;
    reason=reason.trim();
    if(!reason){showToast('请输入事由');return}
  }else{
    const outOfRangeNow=!isWithinRange(currentPos);
    const context=OT.clockOutReviewContext(last,outOfRangeNow,dateKey(new Date()));
    if(context.required){
      reason=await OT.showClockOutNoteModal(context);
      if(reason===null)return;
      reason=reason.trim();
      if(!reason){showToast('请填写下班说明');return}
    }
  }
  if(!isWithinRange(currentPos)){showConfirmModal('⚠️ 范围外打卡','不在范围内，是否记录？',async()=>{await doClock(type,true,reason)},()=>{});return}
  await doClock(type,false,reason);
};

OT.doClock = async function doClock(type,outOfRange,note){
  try{
    const d=await api('/clock',{method:'POST',body:JSON.stringify({type,lat:currentPos?.lat,lng:currentPos?.lng,accuracy:currentPos?.accuracy,outOfRange,note})});
    const record={user_id:currentUser.username,date:d.date,time_str:d.time,ts:Date.now(),type,lat:currentPos?.lat,lng:currentPos?.lng,accuracy:currentPos?.accuracy,out_of_range:d.outOfRange?1:0,note};
    allRecords.push(record);
    let durationText='';
    if(type==='out'){
      const clockIn=OT.getCurrentClockIn(allRecords,record);
      if(clockIn)durationText=OT.formatMinutes(Math.max(0,Math.round((record.ts-clockIn.ts)/60000)));
    }
    OT.showLastClockResult(record,durationText);
    updateClockButton();updateTodayTimeline();updateHeaderStats();renderCalendar();renderStats();
    showToast(type==='in'?'✅ 已记录上班':'✅ 已记录下班');
  }catch(e){showToast('❌ '+e.message)}
};

OT.updateTodayTimeline = function updateTodayTimeline(){
  const t=dateKey(new Date()),recs=allRecords.filter(r=>r.date===t).sort((a,b)=>a.ts-b.ts),card=document.getElementById('today-card'),tl=document.getElementById('today-timeline');
  if(!recs.length){card.style.display='none';return}
  card.style.display='block';
  const last=recs[recs.length-1];
  const statusHtml=last.type==='in'?`<div class="timeline-status">进行中：${OT.formatMinutes(Math.max(0,Math.floor((Date.now()-last.ts)/60000)))}</div>`:'';
  tl.innerHTML=statusHtml+recs.map(r=>`<div class="timeline-item ${r.type==='out'?'out':''}"><div class="timeline-label">${r.type==='in'?'上班打卡':'下班打卡'}</div><div class="timeline-time">${OT.escapeHtml(r.time_str||'')}</div>${OT.actualLocationHtml(r)}${r.note?`<div class="note-text">${OT.escapeHtml(r.note)}</div>`:''}</div>`).join('');
  document.getElementById('today-overtime').textContent=OT.formatMinutes(OT.calcTodayOT(recs,new Date(t+'T12:00:00')));
};
