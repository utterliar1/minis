"""考勤助手 - 后端 API"""
import os, json, hashlib, time, sqlite3, smtplib, secrets
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from email.utils import formataddr
from datetime import datetime, timedelta, timezone
BJ_TZ = timezone(timedelta(hours=8))
def bj_now(): return datetime.now(BJ_TZ)
def bj_from_ts(ts): return datetime.fromtimestamp(ts, BJ_TZ)
from functools import wraps
from threading import Timer
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
import jwt

app = Flask(__name__, static_folder='../frontend', static_url_path='')
CORS(app)
SECRET = os.environ.get('JWT_SECRET', 'ot-tracker-2026')
DB_PATH = os.environ.get('DB_PATH', '/data/overtime.db')

# ==================== Database ====================
def get_db():
    os.makedirs(os.path.dirname(DB_PATH), exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA journal_mode=WAL")
    conn.execute("PRAGMA foreign_keys=ON")
    return conn

def init_db():
    conn = get_db()
    conn.executescript('''
        CREATE TABLE IF NOT EXISTS users (
            username TEXT PRIMARY KEY, display_name TEXT NOT NULL,
            password_hash TEXT NOT NULL, salt TEXT NOT NULL,
            role TEXT DEFAULT 'user', created_at INTEGER
        );
        CREATE TABLE IF NOT EXISTS records (
            id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT NOT NULL,
            date TEXT, time_str TEXT, ts INTEGER, type TEXT,
            lat REAL, lng REAL, accuracy REAL, out_of_range INTEGER DEFAULT 0, note TEXT,
            FOREIGN KEY (user_id) REFERENCES users(username) ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS settings (
            id INTEGER PRIMARY KEY CHECK (id=1), data TEXT
        );
        CREATE TABLE IF NOT EXISTS whitelist (
            name TEXT PRIMARY KEY,
            used INTEGER DEFAULT 0, used_by TEXT, created_at INTEGER
        );
        CREATE TABLE IF NOT EXISTS email_config (
            id INTEGER PRIMARY KEY CHECK (id=1),
            smtp_host TEXT, smtp_port INTEGER DEFAULT 465,
            smtp_user TEXT, smtp_pass TEXT, smtp_ssl INTEGER DEFAULT 1,
            sender_name TEXT DEFAULT '考勤助手',
            recipients TEXT DEFAULT '[]',
            schedule_hour INTEGER DEFAULT 9, schedule_minute INTEGER DEFAULT 0,
            enabled INTEGER DEFAULT 0
        );
    ''')
    # Migrate whitelist table: drop old (code-based) if exists, recreate (name-based)
    try:
        has_code = conn.execute("SELECT sql FROM sqlite_master WHERE type='table' AND name='whitelist'").fetchone()
        if has_code and 'code TEXT' in (has_code['sql'] or ''):
            conn.execute("DROP TABLE whitelist")
            conn.execute('''CREATE TABLE IF NOT EXISTS whitelist (
                name TEXT PRIMARY KEY, used INTEGER DEFAULT 0, used_by TEXT, created_at INTEGER
            )''')
            conn.commit()
    except: pass
    # Default admin
    if not conn.execute("SELECT 1 FROM users WHERE username='admin'").fetchone():
        salt = os.urandom(16).hex()
        h = hashlib.sha256(('admin123' + salt).encode()).hexdigest()
        conn.execute("INSERT INTO users VALUES (?,?,?,?,?,?)",
                     ('admin', '管理员', h, salt, 'admin', int(time.time()*1000)))
        conn.commit()
    # Default settings
    if not conn.execute("SELECT 1 FROM settings WHERE id=1").fetchone():
        default = {"locationName":"","lat":None,"lng":None,"radius":500,"gpsAccuracy":100,
                    "workStart":"09:00","workEnd":"18:00","lunchStart":"12:00","lunchEnd":"13:00",
                    "weekdays":[1,2,3,4,5],"holidays":[],"workdays":[],"holidaySyncTime":None,
                    "autoIn":False,"autoOut":False,"minOvertime":30}
        conn.execute("INSERT INTO settings (id,data) VALUES (1,?)", (json.dumps(default),))
        conn.commit()
    # Default email config
    if not conn.execute("SELECT 1 FROM email_config WHERE id=1").fetchone():
        conn.execute("INSERT INTO email_config (id) VALUES (1)")
        conn.commit()
    conn.close()

# ==================== Auth ====================
def hash_pw(pw, salt): return hashlib.sha256((pw + salt).encode()).hexdigest()
def make_token(u): return jwt.encode({"username":u["username"],"role":u["role"],"dn":u["display_name"],"exp":time.time()+86400*30}, SECRET, algorithm="HS256")

def login_required(f):
    @wraps(f)
    def w(*a, **kw):
        token = request.headers.get('Authorization','').replace('Bearer ','')
        if not token: return jsonify(error="未登录"), 401
        try: request.user = jwt.decode(token, SECRET, algorithms=["HS256"])
        except: return jsonify(error="登录已过期"), 401
        return f(*a, **kw)
    return w

def admin_required(f):
    @wraps(f)
    @login_required
    def w(*a, **kw):
        if request.user.get('role') != 'admin': return jsonify(error="无管理员权限"), 403
        return f(*a, **kw)
    return w

# ==================== Static ====================
@app.route('/')
def index(): return send_from_directory(app.static_folder, 'index.html')

# ==================== Auth API ====================
@app.route('/api/login', methods=['POST'])
def api_login():
    d = request.json or {}
    login_id = d.get('username','').strip()
    conn = get_db()
    # 支持用姓名或用户名登录
    u = conn.execute("SELECT * FROM users WHERE username=? OR display_name=?", (login_id, login_id)).fetchone()
    conn.close()
    if not u or hash_pw(d.get('password',''), u['salt']) != u['password_hash']:
        return jsonify(error="用户名或密码错误"), 401
    return jsonify(token=make_token(u), user={"username":u["username"],"displayName":u["display_name"],"role":u["role"]})

@app.route('/api/register', methods=['POST'])
def api_register():
    d = request.json or {}
    display = d.get('displayName','').strip()
    password = d.get('password','')
    if not display: return jsonify(error="请输入姓名"), 400
    if len(password) < 4: return jsonify(error="密码至少4个字符"), 400
    conn = get_db()
    # Check whitelist: name must exist and not be used
    wl = conn.execute("SELECT * FROM whitelist WHERE name=?", (display,)).fetchone()
    if not wl:
        conn.close(); return jsonify(error="你的姓名不在管理员名单中，请联系管理员添加"), 400
    if wl['used']:
        conn.close(); return jsonify(error="该姓名已注册，请直接登录"), 400
    # Check if display name already exists in users
    if conn.execute("SELECT 1 FROM users WHERE display_name=?", (display,)).fetchone():
        conn.close(); return jsonify(error="该姓名已注册"), 400
    # Auto-generate username
    username = display.lower().replace(' ', '') + '_' + secrets.token_hex(3)
    while conn.execute("SELECT 1 FROM users WHERE username=?", (username,)).fetchone():
        username = display.lower().replace(' ', '') + '_' + secrets.token_hex(3)
    salt = os.urandom(16).hex()
    conn.execute("INSERT INTO users VALUES (?,?,?,?,?,?)",
                 (username, display, hash_pw(password, salt), salt, 'user', int(time.time()*1000)))
    conn.execute("UPDATE whitelist SET used=1, used_by=? WHERE name=?", (username, display))
    conn.commit(); conn.close()
    return jsonify(ok=True)

# ==================== Clock API ====================
@app.route('/api/clock', methods=['POST'])
@login_required
def api_clock():
    d = request.json or {}
    uid = request.user['username']
    now = time.time()
    dt = bj_from_ts(now)
    date_str = dt.strftime('%Y-%m-%d')
    time_str = dt.strftime('%H:%M:%S')
    conn = get_db()
    conn.execute("INSERT INTO records (user_id,date,time_str,ts,type,lat,lng,accuracy,out_of_range,note) VALUES (?,?,?,?,?,?,?,?,?,?)",
                 (uid, date_str, time_str, int(now*1000), d.get('type','in'),
                  d.get('lat'), d.get('lng'), d.get('accuracy'), 1 if d.get('outOfRange') else 0, d.get('note','')))
    conn.commit(); conn.close()
    return jsonify(ok=True, date=date_str, time=time_str, type=d.get('type','in'))

@app.route('/api/records')
@login_required
def api_records():
    uid = request.user['username']
    month = request.args.get('month')
    conn = get_db()
    if month:
        rows = conn.execute("SELECT * FROM records WHERE user_id=? AND date LIKE ? ORDER BY ts", (uid, month+'%')).fetchall()
    else:
        rows = conn.execute("SELECT * FROM records WHERE user_id=? ORDER BY ts", (uid,)).fetchall()
    conn.close()
    return jsonify(records=[dict(r) for r in rows])

@app.route('/api/records/all')
@admin_required
def api_records_all():
    conn = get_db()
    rows = conn.execute("SELECT * FROM records ORDER BY ts").fetchall()
    conn.close()
    return jsonify(records=[dict(r) for r in rows])

@app.route('/api/records/all', methods=['DELETE'])
@admin_required
def api_delete_all_records():
    uid = request.user['username']
    conn = get_db()
    conn.execute("DELETE FROM records WHERE user_id=?", (uid,))
    conn.commit(); conn.close()
    return jsonify(ok=True)

# ==================== Admin API ====================
@app.route('/api/users')
@admin_required
def api_users():
    conn = get_db()
    rows = conn.execute("SELECT username,display_name,role,created_at FROM users ORDER BY created_at").fetchall()
    conn.close()
    return jsonify(users=[dict(r) for r in rows])

@app.route('/api/users/<username>/role', methods=['PUT'])
@admin_required
def api_toggle_role(username):
    if username == request.user['username']: return jsonify(error="不能修改自己"), 400
    if username == 'admin': return jsonify(error="不能修改主管理员"), 400
    conn = get_db()
    u = conn.execute("SELECT role FROM users WHERE username=?", (username,)).fetchone()
    if not u: conn.close(); return jsonify(error="用户不存在"), 404
    new_role = 'user' if u['role'] == 'admin' else 'admin'
    conn.execute("UPDATE users SET role=? WHERE username=?", (new_role, username))
    conn.commit(); conn.close()
    return jsonify(ok=True, role=new_role)

@app.route('/api/change-password', methods=['PUT'])
@login_required
def api_change_password():
    d = request.json or {}
    old_pw = d.get('oldPassword', '')
    new_pw = d.get('newPassword', '')
    if len(new_pw) < 4: return jsonify(error="新密码至少4个字符"), 400
    uid = request.user['username']
    conn = get_db()
    u = conn.execute("SELECT * FROM users WHERE username=?", (uid,)).fetchone()
    if not u: conn.close(); return jsonify(error="用户不存在"), 404
    if hash_pw(old_pw, u['salt']) != u['password_hash']:
        conn.close(); return jsonify(error="当前密码错误"), 400
    salt = os.urandom(16).hex()
    h = hash_pw(new_pw, salt)
    conn.execute("UPDATE users SET password_hash=?, salt=? WHERE username=?", (h, salt, uid))
    conn.commit(); conn.close()
    return jsonify(ok=True)

@app.route('/api/users/<username>/password', methods=['PUT'])
@admin_required
def api_reset_password(username):
    d = request.json or {}
    new_pw = d.get('password', '')
    if len(new_pw) < 4: return jsonify(error="密码至少4个字符"), 400
    if username == 'admin': return jsonify(error="不能修改主管理员密码"), 400
    conn = get_db()
    u = conn.execute("SELECT 1 FROM users WHERE username=?", (username,)).fetchone()
    if not u: conn.close(); return jsonify(error="用户不存在"), 404
    salt = os.urandom(16).hex()
    h = hash_pw(new_pw, salt)
    conn.execute("UPDATE users SET password_hash=?, salt=? WHERE username=?", (h, salt, username))
    conn.commit(); conn.close()
    return jsonify(ok=True)

@app.route('/api/users/<username>', methods=['DELETE'])
@admin_required
def api_delete_user(username):
    if username == request.user['username']: return jsonify(error="不能删除自己"), 400
    if username == 'admin': return jsonify(error="不能删除主管理员"), 400
    conn = get_db()
    # 获取用户显示名用于重置白名单
    u = conn.execute("SELECT display_name FROM users WHERE username=?", (username,)).fetchone()
    if u:
        conn.execute("UPDATE whitelist SET used=0, used_by=NULL WHERE name=?", (u['display_name'],))
    conn.execute("DELETE FROM records WHERE user_id=?", (username,))
    conn.execute("DELETE FROM users WHERE username=?", (username,))
    conn.commit(); conn.close()
    return jsonify(ok=True)

# ==================== Whitelist API ====================
@app.route('/api/whitelist')
@admin_required
def api_whitelist():
    conn = get_db()
    rows = conn.execute("SELECT * FROM whitelist ORDER BY created_at DESC").fetchall()
    conn.close()
    return jsonify(items=[dict(r) for r in rows])

@app.route('/api/whitelist', methods=['POST'])
@admin_required
def api_add_whitelist():
    d = request.json or {}
    name = d.get('name','').strip()
    if not name: return jsonify(error="请输入姓名"), 400
    conn = get_db()
    if conn.execute("SELECT 1 FROM whitelist WHERE name=?", (name,)).fetchone():
        conn.close(); return jsonify(error="该姓名已在名单中"), 400
    conn.execute("INSERT INTO whitelist (name,used,created_at) VALUES (?,0,?)",
                 (name, int(time.time()*1000)))
    conn.commit(); conn.close()
    return jsonify(ok=True)

@app.route('/api/whitelist/<path:name>', methods=['DELETE'])
@admin_required
def api_delete_whitelist(name):
    conn = get_db()
    conn.execute("DELETE FROM whitelist WHERE name=? AND used=0", (name,))
    conn.commit(); conn.close()
    return jsonify(ok=True)

# ==================== Email Config API ====================
@app.route('/api/email-config')
@admin_required
def api_get_email_config():
    conn = get_db()
    row = conn.execute("SELECT * FROM email_config WHERE id=1").fetchone()
    conn.close()
    if not row: return jsonify(config={})
    d = dict(row)
    d['recipients'] = json.loads(d.get('recipients','[]'))
    if d.get('smtp_pass'): d['smtp_pass'] = '••••••'  # Mask password
    return jsonify(config=d)

@app.route('/api/email-config', methods=['PUT'])
@admin_required
def api_update_email_config():
    d = request.json or {}
    conn = get_db()
    row = conn.execute("SELECT * FROM email_config WHERE id=1").fetchone()
    cur = dict(row) if row else {}
    # Only update password if explicitly provided and not masked
    if 'smtp_pass' in d and d['smtp_pass'] != '••••••':
        cur['smtp_pass'] = d['smtp_pass']
    for k in ['smtp_host','smtp_port','smtp_user','sender_name','schedule_hour','schedule_minute','enabled']:
        if k in d: cur[k] = d[k]
    if 'recipients' in d: cur['recipients'] = json.dumps(d['recipients'])
    conn.execute("""UPDATE email_config SET smtp_host=?,smtp_port=?,smtp_user=?,smtp_pass=?,
                    smtp_ssl=?,sender_name=?,recipients=?,schedule_hour=?,schedule_minute=?,enabled=?
                    WHERE id=1""",
                 (cur.get('smtp_host',''), cur.get('smtp_port',465), cur.get('smtp_user',''),
                  cur.get('smtp_pass',''), cur.get('smtp_ssl',1), cur.get('sender_name','考勤助手'),
                  cur.get('recipients','[]'), cur.get('schedule_hour',9), cur.get('schedule_minute',0),
                  cur.get('enabled',0)))
    conn.commit(); conn.close()
    schedule_email_task()
    return jsonify(ok=True)

@app.route('/api/email-config/test', methods=['POST'])
@admin_required
def api_test_email():
    d = request.json or {}
    to = d.get('to','')
    if not to: return jsonify(error="请输入测试邮箱"), 400
    ok, msg = send_email(to, "考勤助手 - 测试邮件", "<h3>✅ 邮件发送成功</h3><p>如果你收到这封邮件，说明配置正确。</p>")
    return jsonify(ok=ok, message=msg)

@app.route('/api/email/send-now', methods=['POST'])
@admin_required
def api_send_now():
    ok, msg = send_overtime_report()
    return jsonify(ok=ok, message=msg)

# ==================== Settings API ====================
@app.route('/api/settings')
@login_required
def api_get_settings():
    conn = get_db()
    row = conn.execute("SELECT data FROM settings WHERE id=1").fetchone()
    conn.close()
    return jsonify(settings=json.loads(row['data']) if row else {})

@app.route('/api/settings', methods=['PUT'])
@admin_required
def api_update_settings():
    d = request.json or {}
    conn = get_db()
    row = conn.execute("SELECT data FROM settings WHERE id=1").fetchone()
    cur = json.loads(row['data']) if row else {}
    cur.update(d)
    conn.execute("UPDATE settings SET data=? WHERE id=1", (json.dumps(cur),))
    conn.commit(); conn.close()
    return jsonify(ok=True, settings=cur)

# ==================== Export ====================
@app.route('/api/export')
@login_required
def api_export():
    uid_param = request.args.get('uid', '')
    is_admin = request.user.get('role') == 'admin'
    # 非管理员只能导出自己；管理员不传uid则导出全部
    uid = uid_param if is_admin else request.user['username']
    # 管理员没传uid或传了all，视为导出全部
    if is_admin and (not uid_param or uid_param == 'all'):
        uid = ''
    period = request.args.get('period', 'month')
    conn = get_db()
    from datetime import date as dt_date
    today = bj_now().date()
    date_from = request.args.get('from', '')
    date_to = request.args.get('to', '')
    if date_from and date_to:
        date_range = True
    elif period == 'today':
        date_from = date_to = str(today)
        date_range = True
    elif period == 'month':
        date_from = today.strftime('%Y-%m-01')
        date_to = today.strftime('%Y-%m-31')
        date_range = True
    else:
        date_from = date_to = ''
        date_range = False
    if uid:
        if date_range:
            rows = conn.execute("SELECT r.*,u.display_name FROM records r JOIN users u ON r.user_id=u.username WHERE r.user_id=? AND r.date>=? AND r.date<=? ORDER BY r.ts", (uid, date_from, date_to)).fetchall()
        else:
            rows = conn.execute("SELECT r.*,u.display_name FROM records r JOIN users u ON r.user_id=u.username WHERE r.user_id=? ORDER BY r.ts", (uid,)).fetchall()
    else:
        if date_range:
            rows = conn.execute("SELECT r.*,u.display_name FROM records r JOIN users u ON r.user_id=u.username WHERE r.date>=? AND r.date<=? ORDER BY r.user_id,r.ts", (date_from, date_to)).fetchall()
        else:
            rows = conn.execute("SELECT r.*,u.display_name FROM records r JOIN users u ON r.user_id=u.username ORDER BY r.user_id,r.ts").fetchall()
    conn.close()
    return jsonify(records=[dict(r) for r in rows])

# ==================== Email Sender ====================
def get_settings_data():
    conn = get_db()
    row = conn.execute("SELECT data FROM settings WHERE id=1").fetchone()
    conn.close()
    return json.loads(row['data']) if row else {}

def calc_user_ot(user_id, settings):
    conn = get_db()
    rows = conn.execute("SELECT * FROM records WHERE user_id=? ORDER BY ts", (user_id,)).fetchall()
    conn.close()
    if not rows: return 0
    date_groups = {}
    for r in rows:
        d = r['date']
        if d not in date_groups: date_groups[d] = []
        date_groups[d].append(dict(r))
    total = 0
    for date, recs in date_groups.items():
        dt = datetime.strptime(date, '%Y-%m-%d')
        is_workday = is_working_day(dt, settings)
        if is_workday:
            work_ms = 0
            sorted_recs = sorted(recs, key=lambda x: x['ts'])
            for i, r in enumerate(sorted_recs):
                if r['type'] == 'in':
                    next_out = None
                    for j in range(i+1, len(sorted_recs)):
                        if sorted_recs[j]['type'] == 'out':
                            next_out = sorted_recs[j]; break
                    if next_out:
                        dur = next_out['ts'] - r['ts']
                        in_min = datetime.fromtimestamp(r['ts']/1000).hour*60 + datetime.fromtimestamp(r['ts']/1000).minute
                        out_min = datetime.fromtimestamp(next_out['ts']/1000).hour*60 + datetime.fromtimestamp(next_out['ts']/1000).minute
                        l_s = time_to_min(settings.get('lunchStart','12:00'))
                        l_e = time_to_min(settings.get('lunchEnd','13:00'))
                        if in_min < l_s and out_min > l_e: dur -= (l_e - l_s) * 60000
                        work_ms += dur
            work_min = get_work_minutes(settings)
            total += max(0, round((work_ms - work_min * 60000) / 60000))
        else:
            sorted_recs = sorted(recs, key=lambda x: x['ts'])
            for i, r in enumerate(sorted_recs):
                if r['type'] == 'in':
                    next_out = None
                    for j in range(i+1, len(sorted_recs)):
                        if sorted_recs[j]['type'] == 'out':
                            next_out = sorted_recs[j]; break
                    if next_out:
                        total += round((next_out['ts'] - r['ts']) / 60000)
    return total

def is_working_day(dt, settings):
    dk = dt.strftime('%Y-%m-%d')
    if dk in settings.get('holidays', []): return False
    if dk in settings.get('workdays', []): return True
    return dt.weekday() in settings.get('weekdays', [0,1,2,3,4])

def time_to_min(t):
    if not t: return 720
    h, m = t.split(':'); return int(h)*60 + int(m)

def get_work_minutes(s):
    return time_to_min(s.get('workEnd','18:00')) - time_to_min(s.get('workStart','09:00')) - (time_to_min(s.get('lunchEnd','13:00')) - time_to_min(s.get('lunchStart','12:00')))

def format_min(m):
    h, mm = divmod(m, 60)
    if h == 0: return f"{mm}分钟"
    if mm == 0: return f"{h}小时"
    return f"{h}小时{mm}分钟"

def send_email(to, subject, html_body):
    conn = get_db()
    row = conn.execute("SELECT * FROM email_config WHERE id=1").fetchone()
    conn.close()
    if not row: return False, "未配置邮件"
    cfg = dict(row)
    if not cfg.get('smtp_host') or not cfg.get('smtp_user'):
        return False, "SMTP 未配置"
    try:
        msg = MIMEMultipart('alternative')
        msg['From'] = formataddr((cfg.get('sender_name',''), cfg['smtp_user']), 'utf-8')
        msg['To'] = to
        msg['Subject'] = subject
        msg.attach(MIMEText(html_body, 'html', 'utf-8'))
        port = int(cfg.get('smtp_port', 465) or 465)
        use_ssl = cfg.get('smtp_ssl', 1)
        if use_ssl or port == 465:
            server = smtplib.SMTP_SSL(cfg['smtp_host'], port, timeout=15)
        else:
            server = smtplib.SMTP(cfg['smtp_host'], port, timeout=15)
            server.starttls()
        if cfg.get('smtp_user') and cfg.get('smtp_pass'):
            server.login(cfg['smtp_user'], cfg['smtp_pass'])
        server.sendmail(cfg['smtp_user'], [to], msg.as_string())
        server.quit()
        return True, "发送成功"
    except smtplib.SMTPAuthenticationError:
        return False, "邮箱认证失败，请检查发件邮箱和密码/授权码"
    except smtplib.SMTPConnectError:
        return False, "无法连接SMTP服务器，请检查服务器地址和端口"
    except smtplib.SMTPServerDisconnected:
        return False, "SMTP服务器断开连接，请检查SSL设置"
    except Exception as e:
        return False, f"发送失败: {str(e)[:200]}"

def send_overtime_report():
    settings = get_settings_data()
    conn = get_db()
    cfg = conn.execute("SELECT * FROM email_config WHERE id=1").fetchone()
    conn.close()
    if not cfg: return False, "邮件未配置"
    cfg = dict(cfg)
    recipients = json.loads(cfg.get('recipients','[]'))
    if not recipients: return False, "无收件人"
    conn = get_db()
    users = conn.execute("SELECT username,display_name FROM users WHERE role='user'").fetchall()
    conn.close()
    now = bj_now()
    month_str = now.strftime('%Y-%m')
    rows_html = ""
    for u in users:
        ot = calc_user_ot(u['username'], settings)
        color = "#DC2626" if ot > 0 else "#16A34A"
        rows_html += f"<tr><td style='padding:8px 12px;border:1px solid #E2E8F0'>{u['display_name']}</td><td style='padding:8px 12px;border:1px solid #E2E8F0;color:{color};font-weight:600;text-align:center'>{format_min(ot)}</td></tr>"
    if not rows_html:
        rows_html = "<tr><td colspan='2' style='padding:12px;text-align:center;color:#94A3B8'>本月暂无打卡记录</td></tr>"
    html = f"""<div style="font-family:-apple-system,sans-serif;max-width:600px;margin:0 auto">
        <h2 style="color:#4F46E5">📊 {now.strftime('%Y年%m月')} 工时统计报告</h2>
        <p style="color:#64748B">自动生成于 {now.strftime('%Y-%m-%d %H:%M')}</p>
        <table style="width:100%;border-collapse:collapse;margin-top:16px">
        <tr style="background:#F8FAFC"><th style="padding:8px 12px;border:1px solid #E2E8F0;text-align:left">成员</th><th style="padding:8px 12px;border:1px solid #E2E8F0;text-align:center">本月工时</th></tr>
        {rows_html}
        </table>
        <p style="color:#94A3B8;font-size:12px;margin-top:24px">— 考勤助手自动发送</p></div>"""
    errors = []
    for to in recipients:
        ok, msg = send_email(to, f"📊 {now.strftime('%Y年%m月')} 工时统计", html)
        if not ok: errors.append(f"{to}: {msg}")
    if errors:
        return False, "; ".join(errors)
    return True, f"已发送给 {len(recipients)} 位收件人"

# ==================== Email Scheduler ====================
_scheduler_timer = None

def schedule_email_task():
    global _scheduler_timer
    if _scheduler_timer: _scheduler_timer.cancel()
    conn = get_db()
    cfg = conn.execute("SELECT * FROM email_config WHERE id=1").fetchone()
    conn.close()
    if not cfg or not cfg['enabled']: return
    cfg = dict(cfg)
    now = bj_now()
    target = now.replace(hour=cfg.get('schedule_hour',9), minute=cfg.get('schedule_minute',0), second=0, microsecond=0)
    if target <= now: target += timedelta(days=1)
    delay = (target - now).total_seconds()
    def job():
        send_overtime_report()
        schedule_email_task()  # Reschedule for next day
    _scheduler_timer = Timer(delay, job)
    _scheduler_timer.daemon = True
    _scheduler_timer.start()

# ==================== Init & Run ====================
init_db()
schedule_email_task()

if __name__ == '__main__':
    port = int(os.environ.get('PORT', 5000))
    app.run(host='0.0.0.0', port=port, debug=False)
