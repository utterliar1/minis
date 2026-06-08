// Admin dashboard and settings
var OT = window.OT = window.OT || {};

OT.getAdminPeriod = function getAdminPeriod(){
  const period=document.getElementById('export-period')?.value||'month';
  const today=OT.dateKey(new Date());
  if(period==='today')return {period,label:'今日',from:today,to:today};
  if(period==='week'){
    const start=new Date(today+'T12:00:00');
    const day=start.getDay();
    const diff=day===0?-6:1-day;
    start.setDate(start.getDate()+diff);
    return {period,label:'本周',from:OT.calendarKey(start),to:today};
  }
  if(period==='month')return {period,label:'本月',from:today.slice(0,7)+'-01',to:today.slice(0,7)+'-31'};
  return {period,label:'全部',from:'',to:''};
};

OT.inAdminPeriod = function inAdminPeriod(record, range){
  if(!range.from||!range.to)return true;
  return record.date>=range.from&&record.date<=range.to;
};

OT.loadDashboard = async function loadDashboard(){
  try{
    const [recsData, usersData]=await Promise.all([api('/records/all'),api('/users')]);
    const records=recsData.records||[];
    const users=usersData.users||[];
    const range=OT.getAdminPeriod();
    const scopedRecords=records.filter(r=>OT.inAdminPeriod(r,range));
    const today=dateKey(new Date());
    const todayObj=new Date(today+'T12:00:00');
    const todayRecs=records.filter(r=>r.date===today);
    const sumRecords=function(recs){
      const dg={};
      recs.forEach(r=>{if(!dg[r.date])dg[r.date]=[];dg[r.date].push(r)});
      let total=0;Object.entries(dg).forEach(([d,dayRecs])=>{total+=calcTodayOT(dayRecs,new Date(d+'T12:00:00'))});
      return total;
    };
    
    // Build table
    const uList=users.filter(u=>u.username!=='admin');
    let rows='';
    for(const u of uList){
      const uToday=todayRecs.filter(r=>r.user_id===u.username);
      const uScoped=scopedRecords.filter(r=>r.user_id===u.username);
      const lastRec=[...uToday].sort((a,b)=>a.ts-b.ts).pop();
      let statusText='<span class="status-dot status-none"></span>未打卡';
      if(lastRec){
        if(lastRec.type==='in')statusText='<span class="status-dot status-in"></span>上班中';
        else statusText='<span class="status-dot status-out"></span>已下班';
      }
      const todayOT=calcTodayOT(uToday,todayObj);
      const rangeOT=sumRecords(uScoped);
      rows+=`<tr><td style="font-weight:600">${escapeHtml(u.display_name)}</td><td>${statusText}</td><td class="${todayOT>0?'ot-positive':'ot-zero'}">${fmtMin(todayOT)}</td><td class="${rangeOT>0?'ot-positive':'ot-zero'}" style="font-weight:600">${fmtMin(rangeOT)}</td></tr>`;
    }
    if(!rows)rows='<tr><td colspan="4" style="text-align:center;color:var(--text-sec);padding:20px">暂无成员</td></tr>';
    
    // Summary
    let totalTodayOT=0,totalRangeOT=0;
    uList.forEach(u=>{
      const uToday=todayRecs.filter(r=>r.user_id===u.username);
      const uScoped=scopedRecords.filter(r=>r.user_id===u.username);
      totalTodayOT+=calcTodayOT(uToday,todayObj);
      totalRangeOT+=sumRecords(uScoped);
    });
    document.getElementById('header-stats').innerHTML=`
      <div class="stat-item"><div class="stat-value">${uList.length}</div><div class="stat-label">成员数</div></div>
      <div class="stat-item"><div class="stat-value">${fmtMin(totalTodayOT)}</div><div class="stat-label">今日团队工时</div></div>
      <div class="stat-item"><div class="stat-value">${fmtMin(totalRangeOT)}</div><div class="stat-label">${range.label}团队工时</div></div>`;
    // Trend data (last 30 days)
    const trendData=[];const todayDate=new Date();
    for(let i=29;i>=0;i--){
      const dd=new Date(todayDate);dd.setDate(dd.getDate()-i);
      const dk=dateKey(dd);
      const dayRecs=records.filter(r=>r.date===dk);
      const dg2={};dayRecs.forEach(r=>{if(!dg2[r.date])dg2[r.date]=[];dg2[r.date].push(r)});
      let dayOT=0;Object.entries(dg2).forEach(([d,recs])=>{dayOT+=calcTodayOT(recs,new Date(d+'T12:00:00'))});
      trendData.push({date:dk,label:`${dd.getMonth()+1}/${dd.getDate()}`,ot:dayOT});
    }
    const maxOT=Math.max(...trendData.map(d=>d.ot),1);
    const trendHTML=`<div class="card" style="margin-top:16px"><div class="card-title">📈 近30天工时趋势</div>
      <div style="overflow-x:auto"><div style="display:flex;align-items:flex-end;gap:2px;height:120px;min-width:${trendData.length*20}px">
      ${trendData.map(d=>`<div title="${d.label}: ${fmtMin(d.ot)}" style="flex:1;min-width:14px;background:${d.ot>0?'linear-gradient(to top,var(--primary),var(--primary-light))':'#E2E8F0'};border-radius:3px 3px 0 0;height:${Math.max(2,d.ot/maxOT*100)}%"></div>`).join('')}
      </div><div style="display:flex;gap:2px;min-width:${trendData.length*20}px;margin-top:4px">
      ${trendData.map((d,i)=>`<div style="flex:1;min-width:14px;text-align:center;font-size:9px;color:var(--text-sec);${i%5!==0?'visibility:hidden':''}">${d.label}</div>`).join('')}
      </div></div></div>`;
    // Ranking
    const rankData=uList.map(u=>{
      const uScoped=scopedRecords.filter(r=>r.user_id===u.username);
      return{name:u.display_name,ot:sumRecords(uScoped)}
    }).sort((a,b)=>b.ot-a.ot);
    const avgOT=rankData.length?Math.round(rankData.reduce((s,r)=>s+r.ot,0)/rankData.length):0;
    const rankHTML=`<div class="card" style="margin-top:16px"><div class="card-title">🏆 ${range.label}工时排行</div>
      <div style="margin-bottom:12px;padding:8px 12px;background:#EEF2FF;border-radius:8px;font-size:13px">团队平均: <b style="color:var(--accent)">${fmtMin(avgOT)}</b>/人</div>
      ${rankData.map((r,i)=>{
        const medals=['🥇','🥈','🥉'];
        const bar=Math.max(2,r.ot/Math.max(...rankData.map(x=>x.ot),1)*100);
        const color=r.ot>avgOT*1.5?'var(--danger)':r.ot>avgOT?'var(--accent)':'var(--success)';
        return`<div style="display:flex;align-items:center;gap:8px;margin-bottom:8px">
          <span style="width:24px;text-align:center;font-size:16px">${medals[i]||(i+1)}</span>
          <span style="width:60px;font-size:13px;font-weight:600">${escapeHtml(r.name)}</span>
          <div style="flex:1;height:16px;background:#F1F5F9;border-radius:4px;overflow:hidden">
            <div style="height:100%;width:${bar}%;background:${color};border-radius:4px"></div>
          </div>
          <span style="width:50px;text-align:right;font-size:12px;font-weight:600;color:${color}">${fmtMin(r.ot)}</span>
        </div>`;
      }).join('')}
    </div>`;
    document.getElementById('dash-content').innerHTML=`<div class="note-text" style="margin-bottom:8px">当前范围：${range.label}</div><table class="dash-table"><thead><tr><th>成员</th><th>状态</th><th>今日</th><th>${range.label}</th></tr></thead><tbody>${rows}</tbody></table>`+trendHTML+rankHTML;
    
    // Export dropdown
    const sel=document.getElementById('export-scope');
    sel.innerHTML='<option value="all">所有人</option>'+uList.map(u=>`<option value="${escapeHtml(u.username)}">${escapeHtml(u.display_name)}</option>`).join('');
  }catch(e){document.getElementById('dash-content').innerHTML='<div class="empty-state"><span class="note-text">加载失败</span></div>'}
};

OT.showExportDialog = function showExportDialog(){
  const isAdmin=currentUser&&currentUser.role==='admin';
  let html=`<div class="modal-title">📥 导出工时报表</div>`;
  if(isAdmin){
    html+=`<div style="margin-bottom:12px"><label style="font-size:13px;color:var(--text-sec);display:block;margin-bottom:4px">人员</label><select id="exp-uid" style="width:100%;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;font-family:inherit"><option value="all">所有人</option></select></div>`;
  }
  html+=`<div style="margin-bottom:12px"><label style="font-size:13px;color:var(--text-sec);display:block;margin-bottom:4px">时间范围</label><select id="exp-period" style="width:100%;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:14px;font-family:inherit"><option value="month">本月</option><option value="today">今日</option><option value="week">本周</option><option value="all">全部</option><option value="range">自定义日期...</option></select></div>`;
  html+=`<div id="exp-range-box" style="display:none;margin-bottom:12px"><div style="display:flex;gap:8px"><input type="date" id="exp-from" style="flex:1;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:13px;font-family:inherit"><input type="date" id="exp-to" style="flex:1;padding:8px;border:1.5px solid var(--border);border-radius:8px;font-size:13px;font-family:inherit"></div></div>`;
  html+=`<button class="btn btn-primary btn-block" onclick="doUnifiedExport()">📥 导出 CSV</button>`;
  showModal(html);
  if(isAdmin){api('/users').then(d=>{const sel=document.getElementById('exp-uid');d.users.filter(u=>u.username!=='admin').forEach(u=>{sel.innerHTML+=`<option value="${escapeHtml(u.username)}">${escapeHtml(u.display_name)}</option>`})})}
  document.getElementById('exp-period').addEventListener('change',function(){document.getElementById('exp-range-box').style.display=this.value==='range'?'block':'none'});
};

OT.doUnifiedExport = async function doUnifiedExport(){
  const isAdmin=currentUser&&currentUser.role==='admin';
  const uid=isAdmin?document.getElementById('exp-uid').value:'';
  const period=document.getElementById('exp-period').value;
  let from='',to='';
  if(period==='range'){from=document.getElementById('exp-from').value;to=document.getElementById('exp-to').value;if(!from||!to){showToast('请选择日期范围');return}}
  const ok=await OT.exportCsv(uid,period,from,to);
  if(ok)closeModalDirect();
};

OT.doExport = async function doExport(){
  if(!(currentUser&&currentUser.role==='admin')){showExportDialog();return}
  const scope=document.getElementById('export-scope')?.value||'all';
  const range=OT.getAdminPeriod();
  await OT.exportCsv(scope,range.period,range.from,range.to);
};

OT.exportCsv = async function exportCsv(uid, period, from='', to=''){
  try{
    let url='/export?';
    if(uid&&uid!=='all')url+=`uid=${encodeURIComponent(uid)}&`;
    if(period==='range')url+=`from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
    else url+=`period=${encodeURIComponent(period)}`;
    const data=await api(url);
    const recs=data.records||[];
    if(!recs.length){showToast('暂无数据');return false}
    // Group by (display_name, date)
    const groups={};
    recs.forEach(r=>{
      const key=(r.display_name||r.user_id)+'|'+r.date;
      if(!groups[key])groups[key]={name:r.display_name||r.user_id,date:r.date,records:[]};
      groups[key].records.push(r);
    });
    const header='姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)\n';
    const rows=Object.values(groups).map(g=>{
      const d=new Date(g.date+'T12:00:00');
      const wd=['周日','周一','周二','周三','周四','周五','周六'][d.getDay()];
      const sorted=g.records.sort((a,b)=>a.ts-b.ts);
      const fi=sorted.find(r=>r.type==='in');
      const lo=[...sorted].reverse().find(r=>r.type==='out');
      const reasons=sorted.filter(r=>r.note).map(r=>r.note).filter(Boolean).join('; ');
      const remote=sorted.some(r=>Number(r.out_of_range)===1)?'是':'';
      const isWork=isWorkingDay(d);
      const ot=calcTodayOT(sorted,d);
      const hrs=ot>=60?Math.floor(ot/60)+'h'+(ot%60?ot%60+'m':''):ot+'m';
      return [csvCell(g.name),csvCell(g.date),csvCell(wd),csvCell(fi?(fi.time_str||'').slice(0,5):''),csvCell(lo?(lo.time_str||'').slice(0,5):''),csvCell(isWork?'工作日':'休息日'),csvCell(reasons),csvCell(remote),ot,csvCell(hrs)].join(',');
    }).join('\n');
    downloadBlob(new Blob(['\uFEFF'+header+rows],{type:'text/csv;charset=utf-8'}),`工时报表_${uid||'me'}_${period}_${dateKey(bjNow())}.csv`);
    showToast('📥 已导出');
    return true;
  }catch(e){showToast('❌ '+e.message);return false}
};

OT.doOldExport = async function doOldExport(){
  const scope=document.getElementById('export-scope')?.value||'all';
  const range=OT.getAdminPeriod();
  await OT.exportCsv(scope,range.period,range.from,range.to);
};

OT.renderSettings = function renderSettings(){
  document.getElementById('set-location-name').textContent=settings.locationName||'未设置';
  document.getElementById('set-radius').value=settings.radius||500;
  document.getElementById('set-work-start').value=settings.workStart||'09:00';
  document.getElementById('set-work-end').value=settings.workEnd||'18:00';
  document.getElementById('set-lunch-start').value=settings.lunchStart||'12:00';
  document.getElementById('set-lunch-end').value=settings.lunchEnd||'13:00';
  const dn=['日','一','二','三','四','五','六'];
  document.getElementById('set-weekdays').innerHTML=[0,1,2,3,4,5,6].map(d=>`<button class="weekday-chip ${(settings.weekdays||[1,2,3,4,5]).includes(d)?'on':''}" onclick="toggleWeekday(${d})">${dn[d]}</button>`).join('');
  renderHolidayTags();
  document.getElementById('info-worktime').textContent=`${settings.workStart||'09:00'} - ${settings.workEnd||'18:00'}`;
  document.getElementById('info-lunch').textContent=`${settings.lunchStart||'12:00'} - ${settings.lunchEnd||'13:00'}`;
  document.getElementById('info-radius').textContent=`${settings.radius||500} 米`;
  if(currentUser&&currentUser.role==='admin'){loadWhitelist();loadUserList();loadEmailConfig()}
};

OT.toggleWeekday = function toggleWeekday(d){if(!settings.weekdays)settings.weekdays=[1,2,3,4,5];const i=settings.weekdays.indexOf(d);if(i>=0)settings.weekdays.splice(i,1);else settings.weekdays.push(d);settings.weekdays.sort()};

OT.saveSettings = async function saveSettings(){
  settings.radius=+document.getElementById('set-radius').value;settings.workStart=document.getElementById('set-work-start').value;settings.workEnd=document.getElementById('set-work-end').value;settings.lunchStart=document.getElementById('set-lunch-start').value;settings.lunchEnd=document.getElementById('set-lunch-end').value;
  try{await api('/settings',{method:'PUT',body:JSON.stringify(settings)});showToast('✅ 设置已保存')}catch(e){showToast('❌ 保存失败')}
};

OT.changeMyPassword = async function changeMyPassword(){
  const old=document.getElementById('change-old-pw').value,nw=document.getElementById('change-new-pw').value,nw2=document.getElementById('change-new-pw2').value;
  if(!old){showToast('请输入当前密码');return}if(!nw||nw.length<4){showToast('新密码至少4位');return}if(nw!==nw2){showToast('两次密码不一致');return}
  try{await api('/change-password',{method:'PUT',body:JSON.stringify({oldPassword:old,newPassword:nw})});showToast('✅ 密码已修改');document.getElementById('change-old-pw').value='';document.getElementById('change-new-pw').value='';document.getElementById('change-new-pw2').value=''}catch(e){showToast('❌ '+e.message)}
};

OT.toggleSection = function toggleSection(name){
  const el=document.getElementById(name+'-list-container');
  const icon=document.getElementById(name+'-toggle-icon');
  if(!el||!icon)return;
  const show=el.style.display==='none';
  el.style.display=show?'':'none';
  const count=icon.dataset.count||'0';
  icon.textContent=show?'▴ 收起':'▾ 展开 ('+count+'人)';
};

OT.loadWhitelist = async function loadWhitelist(){
  const c=document.getElementById('wl-list-container');
  try{const d=await api('/whitelist');if(!d.items.length){c.innerHTML='<div class="setting-item"><span class="note-text" style="color:var(--text-sec)">暂无成员</span></div>';return}
    c.innerHTML='<div class="setting-group" style="margin:0">'+d.items.map(i=>{const st=i.used?'<span class="wl-status wl-status-used">已注册</span>':'<span class="wl-status wl-status-unused">未注册</span>';const del=!i.used?`<button onclick="deleteWL(decodeURIComponent('${safeToken(i.name)}'))" style="border:none;background:none;color:var(--danger);font-size:16px;cursor:pointer;padding:4px 8px">✕</button>`:'';return`<div class="wl-card"><span class="wl-name">${escapeHtml(i.name)}</span><div style="display:flex;align-items:center;gap:8px">${st}${del}</div></div>`}).join('')+'</div>';
    document.getElementById('wl-toggle-icon').dataset.count=d.items.length;
    document.getElementById('wl-toggle-icon').textContent='▾ 展开 ('+d.items.length+'人)';
  }catch(e){c.innerHTML='<div class="setting-item"><span class="note-text">加载失败</span></div>'}
};

OT.addWhitelist = async function addWhitelist(){
  const n=document.getElementById('wl-name-input').value.trim();if(!n){showToast('请输入姓名');return}
  try{await api('/whitelist',{method:'POST',body:JSON.stringify({name:n})});document.getElementById('wl-name-input').value='';loadWhitelist();showToast(`✅ 已添加「${n}」`)}catch(e){showToast('❌ '+e.message)}
};

OT.deleteWL = async function deleteWL(n){showConfirmModal('移除成员',`确认移除「${n}」？`,async()=>{try{await api('/whitelist/'+encodeURIComponent(n),{method:'DELETE'});loadWhitelist();showToast('已移除')}catch(e){showToast('❌ '+e.message)}})};

OT.loadUserList = async function loadUserList(){
  const c=document.getElementById('users-list-container');
  try{const d=await api('/users');const users=d.users.filter(u=>u.username!=='admin');
    document.getElementById('users-toggle-icon').dataset.count=users.length;
    document.getElementById('users-toggle-icon').textContent='▾ 展开 ('+users.length+'人)';
    if(!users.length){c.innerHTML='<div class="setting-item"><span class="note-text" style="color:var(--text-sec)">暂无注册用户</span></div>';return}
    c.innerHTML='<div class="setting-group" style="margin:0">'+users.map(u=>{const isSelf=u.username===currentUser.username;const isDef=u.username==='admin',un=safeToken(u.username),dn=safeToken(u.display_name),display=escapeHtml(u.display_name||u.username),initial=escapeHtml((u.display_name||u.username)[0]||'');
    return`<div class="user-card"><div class="user-card-info"><div class="user-avatar ${u.role==='admin'?'avatar-admin':'avatar-user'}">${initial}</div><div><div class="user-name">${display}${isSelf?' <span style="color:var(--success);font-size:12px">（我）</span>':''}</div><div class="user-meta">@${escapeHtml(u.username)} · ${u.role==='admin'?'管理员':'成员'}</div></div></div><div class="user-actions">${!isSelf&&!isDef?`<button onclick="toggleRole(decodeURIComponent('${un}'))" title="切换角色">${u.role==='admin'?'👤':'⭐'}</button><button onclick="resetPwd(decodeURIComponent('${dn}'),decodeURIComponent('${un}'))" title="重置密码" style="color:var(--accent)">🔑</button><button onclick="delUser(decodeURIComponent('${un}'))" title="删除" style="color:var(--danger)">🗑</button>`:''}</div></div>`}).join('')+'</div>';}
  catch(e){c.innerHTML=''}
};

OT.toggleRole = async function toggleRole(u){try{await api(`/users/${u}/role`,{method:'PUT'});loadUserList();showToast('✅ 已切换')}catch(e){showToast('❌ '+e.message)}};

OT.resetPwd = function resetPwd(name,username){
  const newPwd=prompt(`重置「${name}」的密码\n输入新密码（至少4位）：`);
  if(!newPwd)return;
  if(newPwd.length<4){showToast('密码至少4个字符');return}
  showConfirmModal('重置密码',`确认将「${name}」的密码重置？`,async()=>{try{await api(`/users/${username}/password`,{method:'PUT',body:JSON.stringify({password:newPwd})});showToast(`✅ 「${name}」密码已重置`)}catch(e){showToast('❌ '+e.message)}})
};

OT.delUser = async function delUser(u){showConfirmModal('删除用户',`确认删除「${u}」及其记录？`,async()=>{try{await api(`/users/${u}`,{method:'DELETE'});loadUserList();showToast('已删除')}catch(e){showToast('❌ '+e.message)}})};

OT.loadEmailConfig = async function loadEmailConfig(){try{const d=await api('/email-config');const c=d.config||{};document.getElementById('email-host').value=c.smtp_host||'';document.getElementById('email-port').value=c.smtp_port||465;document.getElementById('email-user').value=c.smtp_user||'';document.getElementById('email-pass').value='';document.getElementById('email-sender').value=c.sender_name||'考勤助手';document.getElementById('email-recipients').value=(c.recipients||[]).join('\n');document.getElementById('email-hour').value=c.schedule_hour??9;document.getElementById('email-min').value=c.schedule_minute??0;document.getElementById('set-email-enabled').className='toggle'+(c.enabled?' on':'')}catch(e){}};

OT.toggleEmailEnabled = function toggleEmailEnabled(){document.getElementById('set-email-enabled').classList.toggle('on')};

OT.saveEmailConfig = async function saveEmailConfig(){
  const rc=document.getElementById('email-recipients').value.split('\n').map(s=>s.trim()).filter(Boolean),pass=document.getElementById('email-pass').value;
  const body={smtp_host:document.getElementById('email-host').value,smtp_port:+document.getElementById('email-port').value,smtp_user:document.getElementById('email-user').value,sender_name:document.getElementById('email-sender').value,recipients:rc,schedule_hour:+document.getElementById('email-hour').value,schedule_minute:+document.getElementById('email-min').value,enabled:document.getElementById('set-email-enabled').classList.contains('on')?1:0};
  if(pass&&pass!=='••••••')body.smtp_pass=pass;
  try{await api('/email-config',{method:'PUT',body:JSON.stringify(body)});showToast('✅ 邮件配置已保存');loadEmailConfig()}catch(e){showToast('❌ '+e.message)}
};

OT.testEmail = async function testEmail(){const to=prompt('输入测试收件邮箱：');if(!to)return;try{showToast('⏳ 发送中...');const d=await api('/email-config/test',{method:'POST',body:JSON.stringify({to})});showToast(d.ok?'✅ 已发送':'❌ '+d.message)}catch(e){showToast('❌ '+e.message)}};

OT.sendReportNow = async function sendReportNow(){try{showToast('⏳ 生成中...');const d=await api('/email/send-now',{method:'POST'});showToast(d.ok?'✅ '+d.message:'❌ '+d.message)}catch(e){showToast('❌ '+e.message)}};

OT.setCurrentLocation = async function setCurrentLocation(){
  showToast('📍 正在获取位置...');
  try{
    const pos=await new Promise((resolve,reject)=>navigator.geolocation.getCurrentPosition(resolve,reject,{enableHighAccuracy:true,timeout:10000}));
    const lat=pos.coords.latitude,lng=pos.coords.longitude,acc=Math.round(pos.coords.accuracy);
    showModal(`<div class="modal-title">📍 确认打卡位置</div>
      <p style="font-size:14px;color:var(--text-sec);text-align:center;margin-bottom:16px">纬度: ${lat.toFixed(6)}<br>经度: ${lng.toFixed(6)}<br>精度: ${acc}m</p>
      <div class="auth-field"><label>地点名称</label><input type="text" id="loc-name-input" placeholder="如：公司/办公室" value="${escapeHtml(settings.locationName||'')}"></div>
      <button class="btn btn-primary" onclick="confirmLoc(${lat},${lng})">✅ 确认设置</button>`);
  }catch(e){showToast('❌ 获取位置失败: '+e.message)}
};

OT.confirmLoc = async function confirmLoc(lat,lng){
  settings.lat=lat;settings.lng=lng;settings.locationName=document.getElementById('loc-name-input').value||'打卡点';
  try{await api('/settings',{method:'PUT',body:JSON.stringify(settings)});closeModalDirect();renderSettings();showToast('✅ 打卡位置已设置')}catch(e){showToast('❌ 保存失败')}
};

OT.syncHolidays = async function syncHolidays(){
  const btn=document.getElementById('sync-btn'),st=document.getElementById('sync-status'),year=new Date().getFullYear();
  btn.innerHTML='<span class="sync-spin">⏳</span>';btn.disabled=true;st.textContent='同步中...';
  const urls=[`https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/${year}.json`,`https://cdn.jsdelivr.net/gh/NateScarlet/holiday-cn@master/${year}.json`];
  let data=null;for(const u of urls){try{const r=await fetch(u+'?_='+Date.now(),{cache:'no-store'});if(r.ok){data=await r.json();break}}catch(e){continue}}
  if(!data){for(const u of urls){try{const r=await fetch(u.replace(`/${year}.json`,`/${year-1}.json`)+'?_='+Date.now(),{cache:'no-store'});if(r.ok){data=await r.json();break}}catch(e){continue}}}
  if(!data||!data.days){btn.innerHTML='📥 同步';btn.disabled=false;st.textContent='❌ 同步失败';showToast('❌ 同步失败');return}
  const sH=[],sW=[];data.days.forEach(d=>{if(d.isOffDay===true)sH.push(d.date);else if(d.isOffDay===false)sW.push(d.date)});
  const all=new Set([...sH,...sW]);const mH=(settings.holidays||[]).filter(h=>!all.has(h)),mW=(settings.workdays||[]).filter(w=>!all.has(w));
  settings.holidays=[...sH,...mH].sort();settings.workdays=[...sW,...mW].sort();settings.holidaySyncTime=Date.now();
  try{await api('/settings',{method:'PUT',body:JSON.stringify(settings)})}catch(e){}
  btn.innerHTML='📥 同步';btn.disabled=false;
  st.innerHTML=`✅ ${new Date().toLocaleDateString('zh-CN')}（${year}年）`;
  renderHolidayTags();showToast(`✅ ${sH.length} 节假日 + ${sW.length} 调休`);
};

OT.renderHolidayTags = function renderHolidayTags(){
  const ht=document.getElementById('holiday-tags'),wt=document.getElementById('workday-tags'),sg=document.getElementById('holiday-synced-group');
  if(!ht)return;const has=(settings.holidays&&settings.holidays.length)||(settings.workdays&&settings.workdays.length);
  if(sg)sg.style.display=has?'':'none';
  ht.innerHTML=(settings.holidays||[]).map((h,i)=>{const d=new Date(h+'T12:00:00');return`<span class="holiday-tag">${d.getMonth()+1}/${d.getDate()}<button onclick="rmHoliday('holiday',${i})">×</button></span>`}).join('')||'<span class="note-text">暂无</span>';
  const hc=document.getElementById('holiday-count');if(hc)hc.textContent=settings.holidays?settings.holidays.length+' 天':'';
  if(wt){wt.innerHTML=(settings.workdays||[]).map((w,i)=>{const d=new Date(w+'T12:00:00');return`<span class="workday-tag">${d.getMonth()+1}/${d.getDate()}<button onclick="rmHoliday('workday',${i})">×</button></span>`}).join('')||'<span class="note-text">暂无</span>';
  const wc=document.getElementById('workday-count');if(wc)wc.textContent=settings.workdays?settings.workdays.length+' 天':''}
  const sts=document.getElementById('sync-status');if(sts&&settings.holidaySyncTime)sts.innerHTML=`✅ ${new Date(settings.holidaySyncTime).toLocaleDateString('zh-CN')}`;
};

OT.rmHoliday = async function rmHoliday(t,i){if(t==='holiday')settings.holidays.splice(i,1);else settings.workdays.splice(i,1);try{await api('/settings',{method:'PUT',body:JSON.stringify(settings)})}catch(e){}renderHolidayTags()};

OT.addHolidayManual = async function addHolidayManual(){const inp=document.getElementById('holiday-input'),sel=document.getElementById('holiday-type-select');if(!inp.value)return;if(sel.value==='holiday'){if(!settings.holidays)settings.holidays=[];if(!settings.holidays.includes(inp.value))settings.holidays.push(inp.value)}else{if(!settings.workdays)settings.workdays=[];if(!settings.workdays.includes(inp.value))settings.workdays.push(inp.value)}try{await api('/settings',{method:'PUT',body:JSON.stringify(settings)})}catch(e){}renderHolidayTags();inp.value=''};
