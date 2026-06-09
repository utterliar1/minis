// Page routing and bootstrapping
var OT = window.OT = window.OT || {};

OT.showPage = function showPage(name){
  if(name==='settings'&&(!currentUser||currentUser.role!=='admin'))name='clock';
  document.querySelectorAll('.page').forEach(p=>p.classList.remove('active'));
  const el=document.getElementById('page-'+name);if(el)el.classList.add('active');
  document.querySelectorAll('#tab-nav .tab-item').forEach(t=>t.classList.toggle('active',t.dataset.page===name));
  if(name==='records')renderCalendar();if(name==='stats')renderStats();
  if(name==='settings')renderSettings()
  if(name==='email')loadEmailConfig()
  if(name==='members'){loadWhitelist();loadUserList()}
  if(name==='dashboard')loadDashboard();
};

OT.showSettingsTab = function showSettingsTab(n){};

Object.assign(window, {
  addHolidayManual: OT.addHolidayManual,
  addWhitelist: OT.addWhitelist,
  api: OT.api,
  applyRoleAccess: OT.applyRoleAccess,
  actualLocationHtml: OT.actualLocationHtml,
  actualLocationText: OT.actualLocationText,
  bjDateStr: OT.bjDateStr,
  bjNow: OT.bjNow,
  bjTimeStr: OT.bjTimeStr,
  bjWeekday: OT.bjWeekday,
  buildExportCsv: OT.buildExportCsv,
  buildTabNav: OT.buildTabNav,
  calcTodayOT: OT.calcTodayOT,
  calendarKey: OT.calendarKey,
  changeMonth: OT.changeMonth,
  changeMyPassword: OT.changeMyPassword,
  closeModal: OT.closeModal,
  closeModalDirect: OT.closeModalDirect,
  confirmLoc: OT.confirmLoc,
  csvCell: OT.csvCell,
  csvHourText: OT.csvHourText,
  dateKey: OT.dateKey,
  delUser: OT.delUser,
  deleteWL: OT.deleteWL,
  doClock: OT.doClock,
  doExport: OT.doExport,
  doLogin: OT.doLogin,
  doLogout: OT.doLogout,
  doOldExport: OT.doOldExport,
  doRegister: OT.doRegister,
  doUnifiedExport: OT.doUnifiedExport,
  downloadBlob: OT.downloadBlob,
  enterApp: OT.enterApp,
  escapeHtml: OT.escapeHtml,
  exportRowFromGroup: OT.exportRowFromGroup,
  exportMyRecords: OT.exportMyRecords,
  fmtMin: OT.fmtMin,
  formatMinutes: OT.formatMinutes,
  getLastTodayRecord: OT.getLastTodayRecord,
  groupExportRecords: OT.groupExportRecords,
  getWorkMinutes: OT.getWorkMinutes,
  handleClock: OT.handleClock,
  haversineDistance: OT.haversineDistance,
  isWithinRange: OT.isWithinRange,
  isWorkingDay: OT.isWorkingDay,
  loadAllRecords: OT.loadAllRecords,
  loadDashboard: OT.loadDashboard,
  loadEmailConfig: OT.loadEmailConfig,
  loadUserList: OT.loadUserList,
  loadWhitelist: OT.loadWhitelist,
  msToMin: OT.msToMin,
  recordPairs: OT.recordPairs,
  renderCalendar: OT.renderCalendar,
  renderHolidayTags: OT.renderHolidayTags,
  renderRecordsList: OT.renderRecordsList,
  renderSettings: OT.renderSettings,
  renderStats: OT.renderStats,
  resetPwd: OT.resetPwd,
  rmHoliday: OT.rmHoliday,
  safeToken: OT.safeToken,
  saveEmailConfig: OT.saveEmailConfig,
  saveSettings: OT.saveSettings,
  sendReportNow: OT.sendReportNow,
  setCurrentLocation: OT.setCurrentLocation,
  showConfirmModal: OT.showConfirmModal,
  showDayDetail: OT.showDayDetail,
  showExportDialog: OT.showExportDialog,
  showModal: OT.showModal,
  showPage: OT.showPage,
  showSettingsTab: OT.showSettingsTab,
  showToast: OT.showToast,
  startGeoWatch: OT.startGeoWatch,
  switchAuthTab: OT.switchAuthTab,
  syncHolidays: OT.syncHolidays,
  testEmail: OT.testEmail,
  timeToMin: OT.timeToMin,
  toggleEmailEnabled: OT.toggleEmailEnabled,
  toggleEmailIncludeOutOfRange: OT.toggleEmailIncludeOutOfRange,
  toggleEmailScheduleMode: OT.toggleEmailScheduleMode,
  toggleRole: OT.toggleRole,
  toggleSection: OT.toggleSection,
  toggleWeekday: OT.toggleWeekday,
  updateClock: OT.updateClock,
  updateClockButton: OT.updateClockButton,
  updateGeoStatus: OT.updateGeoStatus,
  updateHeaderStats: OT.updateHeaderStats,
  updateTodayTimeline: OT.updateTodayTimeline
});

// ==================== PATH DETECTION ====================
(function(){
  const path = window.location.pathname;
  const isAdminPage = path === '/admin' || path === '/admin/';
  const guideLinks = document.getElementById('guide-links');
  if(isAdminPage){
    // 管理员登录页：隐藏注册 tab，显示管理员指南
    const tabReg = document.getElementById('tab-register');
    if(tabReg) tabReg.style.display = 'none';
    guideLinks.innerHTML = '<a href="/使用指南.html?v=16" style="color:#4F46E5;text-decoration:underline;margin:0 8px">📋 使用指南</a><a href="/管理员使用指南.html?v=16" style="color:#4F46E5;text-decoration:underline;margin:0 8px">🔧 管理员指南</a>';
  } else {
    // 成员登录页：显示注册 tab（需要管理员先加入白名单）
    const tabReg = document.getElementById('tab-register');
    if(tabReg) tabReg.style.display = '';
    guideLinks.innerHTML = '<a href="/使用指南.html?v=16" style="color:#4F46E5;text-decoration:underline;margin:0 8px">📋 使用指南</a>';
  }
})();

if(OT.token&&OT.currentUser)OT.enterApp();

// PWA: Register Service Worker
if('serviceWorker' in navigator){
  navigator.serviceWorker.register('/sw.js').catch(()=>{});
}
