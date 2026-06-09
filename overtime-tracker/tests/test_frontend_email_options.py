import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_frontend_email_options_load_save_and_toggle_schedule_fields():
    script = r"""
const fs = require('fs');
const vm = require('vm');

const elements = {};
const requests = [];
function element(id) {
  if (!elements[id]) {
    elements[id] = {
      id,
      value: '',
      className: '',
      style: {},
      classList: {
        contains(cls) { return (elements[id].className || '').split(/\s+/).includes(cls); },
        toggle(cls) {
          const parts = new Set((elements[id].className || '').split(/\s+/).filter(Boolean));
          if (parts.has(cls)) parts.delete(cls); else parts.add(cls);
          elements[id].className = Array.from(parts).join(' ');
        },
      },
    };
  }
  return elements[id];
}

const sandbox = {
  console,
  location: { origin: 'http://127.0.0.1' },
  localStorage: { getItem() { return null; } },
  setTimeout() {},
  document: {
    getElementById: element,
    createElement() { return { click() {} }; },
  },
  URL: { createObjectURL() { return 'blob:'; }, revokeObjectURL() {} },
};
sandbox.window = sandbox;
vm.createContext(sandbox);
for (const file of ['frontend/js/utils.js', 'frontend/js/admin.js']) {
  vm.runInContext(fs.readFileSync(file, 'utf8'), sandbox, { filename: file });
}
for (const key of Object.keys(sandbox.OT)) sandbox[key] = sandbox.OT[key];

sandbox.OT.showToast = sandbox.showToast = function(){};
sandbox.OT.renderHolidayTags = sandbox.renderHolidayTags = function(){};
sandbox.OT.api = sandbox.api = async function(path, opts) {
  if (path === '/email-config' && !opts) {
    return { config: {
      smtp_host: 'smtp.example.com',
      smtp_port: 465,
      smtp_user: 'sender@example.com',
      sender_name: '????',
      recipients: ['boss@example.com'],
      schedule_hour: 8,
      schedule_minute: 45,
      enabled: 1,
      schedule_frequency: 'weekly',
      schedule_weekday: 5,
      schedule_month_day: 'last',
      report_period: 'last_week',
      report_content: 'summary_csv',
      member_filter: 'with_records',
      include_out_of_range: 1,
    }};
  }
  if (path === '/email-config' && opts && opts.method === 'PUT') {
    requests.push(JSON.parse(opts.body));
    return { ok: true };
  }
  throw new Error(`Unexpected API call ${path}`);
};

(async () => {
  await sandbox.OT.loadEmailConfig();
  if (element('email-frequency').value !== 'weekly') throw new Error('frequency not loaded');
  if (element('email-weekday-row').style.display !== 'flex') throw new Error('weekday row should show');
  if (element('email-month-day-row').style.display !== 'none') throw new Error('month row should hide');
  if (element('email-report-period').value !== 'last_week') throw new Error('report period not loaded');
  if (element('email-report-content').value !== 'summary_csv') throw new Error('report content not loaded');
  if (element('email-member-filter').value !== 'with_records') throw new Error('member filter not loaded');
  if (!element('email-include-out-of-range').className.includes('on')) throw new Error('range toggle not loaded');

  element('email-frequency').value = 'monthly';
  sandbox.OT.toggleEmailScheduleMode();
  if (element('email-weekday-row').style.display !== 'none') throw new Error('weekday row should hide');
  if (element('email-month-day-row').style.display !== 'flex') throw new Error('month row should show');

  await sandbox.OT.saveEmailConfig();
  const body = requests[0];
  if (body.schedule_frequency !== 'monthly') throw new Error(`bad frequency ${body.schedule_frequency}`);
  if (body.schedule_weekday !== 5) throw new Error(`bad weekday ${body.schedule_weekday}`);
  if (body.schedule_month_day !== 'last') throw new Error(`bad month day ${body.schedule_month_day}`);
  if (body.report_period !== 'last_week') throw new Error(`bad period ${body.report_period}`);
  if (body.report_content !== 'summary_csv') throw new Error(`bad content ${body.report_content}`);
  if (body.member_filter !== 'with_records') throw new Error(`bad member filter ${body.member_filter}`);
  if (body.include_out_of_range !== 1) throw new Error(`bad include range ${body.include_out_of_range}`);
})().catch(err => {
  console.error(err);
  process.exit(1);
});
"""
    result = subprocess.run(
        ["node", "-e", script],
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )

    assert result.returncode == 0, result.stderr
