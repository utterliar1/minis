# Export Report Format Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make personal and manager CSV exports use one consistent format with remote markers, reasons, hour formatting, and summary rows.

**Architecture:** Add shared CSV-building helpers in `frontend/js/utils.js`, then have both `frontend/js/admin.js` and `frontend/js/records.js` call those helpers. Keep backend export payload unchanged because it already returns raw records with `note` and `out_of_range`.

**Tech Stack:** Native JavaScript frontend, Node VM tests under `pytest`, existing Flask/SQLite backend tests.

---

### Task 1: Shared CSV Builder Tests

**Files:**
- Create: `overtime-tracker/tests/test_export_csv_format.py`
- Read: `overtime-tracker/frontend/js/utils.js`
- Read: `overtime-tracker/frontend/js/stats.js`

- [ ] **Step 1: Write failing tests**

Create `tests/test_export_csv_format.py` with a Node VM script that loads `frontend/js/utils.js` and `frontend/js/stats.js`, sets `settings`, and calls `OT.buildExportCsv(records)`.

Expected CSV must include:

```text
姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)
"Alice","2026-06-09","周二","07:30","19:00","工作日","早段; 远程说明","是",120,"2h"
"Bob","2026-06-13","周六","09:00","12:00","休息日","周末支持","",180,"3h"
"汇总","","","","","","","远程天数 1",300,"5h"
```

- [ ] **Step 2: Run red test**

Run:

```powershell
python -m pytest tests/test_export_csv_format.py -q
```

Expected: FAIL because `OT.buildExportCsv` does not exist.

### Task 2: Shared CSV Builder Implementation

**Files:**
- Modify: `overtime-tracker/frontend/js/utils.js`
- Read: `overtime-tracker/frontend/js/stats.js`

- [ ] **Step 1: Implement helper functions**

Add these helpers to `frontend/js/utils.js`:

```javascript
OT.csvHourText = function csvHourText(minutes){
  const m=Math.max(0,Number(minutes)||0),h=Math.floor(m/60),mm=m%60;
  if(h<=0)return `${mm}m`;
  return mm?`${h}h${mm}m`:`${h}h`;
};

OT.groupExportRecords = function groupExportRecords(records){
  const groups={};
  (records||[]).forEach(r=>{
    const key=(r.display_name||r.user_id||'')+'|'+r.date;
    if(!groups[key])groups[key]={name:r.display_name||r.user_id||'',date:r.date,records:[]};
    groups[key].records.push(r);
  });
  return Object.values(groups).sort((a,b)=>a.date===b.date?String(a.name).localeCompare(String(b.name)):a.date.localeCompare(b.date));
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

OT.buildExportCsv = function buildExportCsv(records){
  const rows=OT.groupExportRecords(records).map(OT.exportRowFromGroup);
  const detailLines=rows.map(r=>[
    OT.csvCell(r.name),OT.csvCell(r.date),OT.csvCell(r.weekday),OT.csvCell(r.firstIn),OT.csvCell(r.lastOut),
    OT.csvCell(r.type),OT.csvCell(r.reasons),OT.csvCell(r.remoteText),r.minutes,OT.csvCell(r.hours)
  ].join(','));
  const totalMinutes=rows.reduce((sum,r)=>sum+r.minutes,0);
  const remoteDays=rows.filter(r=>r.remote).length;
  const summary=[OT.csvCell('汇总'),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(''),OT.csvCell(`远程天数 ${remoteDays}`),totalMinutes,OT.csvCell(OT.csvHourText(totalMinutes))].join(',');
  return '姓名,日期,星期,上班,下班,类型,事由,远程,工时(分),工时(h)\n'+detailLines.concat(summary).join('\n');
};
```

- [ ] **Step 2: Run builder tests**

Run:

```powershell
python -m pytest tests/test_export_csv_format.py -q
```

Expected: PASS.

### Task 3: Wire Personal And Manager Exports

**Files:**
- Modify: `overtime-tracker/frontend/js/admin.js`
- Modify: `overtime-tracker/frontend/js/records.js`
- Modify: `overtime-tracker/frontend/js/app.js`
- Modify: `overtime-tracker/frontend/index.html`
- Modify: `overtime-tracker/frontend/sw.js`

- [ ] **Step 1: Update manager export**

Replace duplicated CSV row construction in `OT.exportCsv` with:

```javascript
const csv=OT.buildExportCsv(recs);
downloadBlob(new Blob(['\uFEFF'+csv],{type:'text/csv;charset=utf-8'}),`工时报表_${uid||'me'}_${period}_${dateKey(bjNow())}.csv`);
```

- [ ] **Step 2: Update personal export**

Replace `OT.exportMyRecords` CSV construction in `frontend/js/records.js` with:

```javascript
const csv=OT.buildExportCsv(recs.map(r=>({...r,display_name:(currentUser&&currentUser.displayName)||r.display_name||r.user_id})));
downloadBlob(new Blob(['\uFEFF'+csv],{type:'text/csv;charset=utf-8'}),`我的工时记录_${dateKey(new Date())}.csv`);
```

- [ ] **Step 3: Expose helper functions**

Add `buildExportCsv`, `csvHourText`, `exportRowFromGroup`, and `groupExportRecords` to the `Object.assign(window, {...})` block in `frontend/js/app.js`.

- [ ] **Step 4: Bump static asset version**

Change static asset query strings and service worker cache from `v=7` to `v=8` in `frontend/index.html`, `frontend/js/app.js`, and `frontend/sw.js`.

- [ ] **Step 5: Run JS syntax check**

Run:

```powershell
node --check frontend/sw.js; node --check frontend/js/utils.js; node --check frontend/js/auth.js; node --check frontend/js/stats.js; node --check frontend/js/records.js; node --check frontend/js/clock.js; node --check frontend/js/admin.js; node --check frontend/js/app.js
```

Expected: all commands exit 0.

### Task 4: Docs And Full Verification

**Files:**
- Modify: `overtime-tracker/README.md`
- Modify: `overtime-tracker/管理员使用指南.html`
- Modify: `overtime-tracker/使用指南.html` if personal export wording needs a field list.

- [ ] **Step 1: Update docs**

Document that all CSV exports include the unified columns and a summary row. Do not mention fee or amount fields.

- [ ] **Step 2: Run full tests**

Run:

```powershell
python -m pytest tests -q
```

Expected: all tests pass.

- [ ] **Step 3: Run scans**

Run:

```powershell
rg -n "加班|åŠ ç­|鍔犂彮" frontend backend README.md 使用指南.html 管理员使用指南.html
rg -n "费率|费用|金额" frontend backend README.md 使用指南.html 管理员使用指南.html
```

Expected: no matches in app/docs touched for this feature.

- [ ] **Step 4: Docker/browser verification**

Run Docker from the worktree and verify `http://127.0.0.1:3090/?v=8` loads, then export once as admin and once as a normal user. Confirm both downloaded CSVs contain the unified header and a `汇总` row.
