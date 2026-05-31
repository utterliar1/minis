#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os, re, time, random, json, requests

DATA_FILE = "/ql/data/scripts/data.txt"
MAX_RETRIES = 3
RETRY_DELAY = 1500
RETRY_MULTIPLIER = 1.5

def with_retry(fn, args=(), op_name="", max_retries=MAX_RETRIES):
    retries = 0
    delay = RETRY_DELAY
    while True:
        try:
            return fn(*args)
        except Exception as e:
            retries += 1
            if retries >= max_retries:
                print(op_name + " 失败，已达最大重试次数")
                raise
            delay = int(delay * RETRY_MULTIPLIER)
            print(op_name + " 失败，" + str(delay // 1000) + "秒后重试(" + str(retries) + "/" + str(max_retries) + ")...")
            time.sleep(delay / 1000)

def get_code(phone, pwd):
    def _do():
        pat = re.compile(r'^1\d{10}$')
        is_p = bool(pat.match(phone))
        pp = "+86" + phone if is_p else phone
        tn = "huami_phone" if is_p else "huami"
        url = "https://api-user.huami.com/registrations/" + pp + "/tokens"
        hd = {"content-type": "application/x-www-form-urlencoded;charset=UTF-8","user-agent": "MiFit/6.12.0 (MCE16; Android 16; Density/1.5)","app_name": "com.xiaomi.hm.health"}
        d = {"client_id":"HuaMi","country_code":"CN","json_response":"true","name":pp,"password":pwd,"redirect_uri":"https://s3-us-west-2.amazonaws.com/hm-registration/successsignin.html","state":"REDIRECTION","token":"access"}
        r = requests.post(url, data=d, headers=hd, allow_redirects=False, timeout=15)
        if r.status_code >= 400:
            raise requests.exceptions.HTTPError("HTTP " + str(r.status_code), response=r)
        b = r.json() if r.text else {}
        if b.get("access"):
            return b["access"], tn
        loc = r.headers.get("Location", "")
        m = re.search(r'access=([^&]+)', loc)
        if m:
            return m.group(1), tn
        raise Exception("获取code失败")
    return with_retry(_do, op_name="获取登录码")

def get_token(code, tn):
    def _do():
        url = "https://account.huami.com/v2/client/login"
        hd = {"content-type": "application/x-www-form-urlencoded;charset=UTF-8","user-agent": "MiFit/6.12.0 (MCE16; Android 16; Density/1.5)"}
        d = {"app_name":"com.xiaomi.hm.health","country_code":"CN","code":code,"device_id":"02:00:00:00:00:00","device_model":"android_phone","app_version":"6.12.0","grant_type":"access_token","allow_registration":"false","source":"com.xiaomi.hm.health","third_name":tn}
        r = requests.post(url, data=d, headers=hd, timeout=15)
        r.raise_for_status()
        ti = r.json().get("token_info", {})
        lt, uid = ti.get("login_token"), ti.get("user_id")
        if not lt or not uid:
            raise Exception("获取token失败")
        return lt, uid
    return with_retry(_do, op_name="获取令牌")

def get_apptoken(lt):
    def _do():
        url = "https://account-cn.huami.com/v1/client/app_tokens?app_name=com.xiaomi.hm.health&dn=api-user.huami.com,api-mifit.huami.com,app-analytics.huami.com&login_token=" + lt
        hd = {"user-agent": "MiFit/6.12.0 (MCE16; Android 16; Density/1.5)"}
        r = requests.get(url, headers=hd, timeout=15)
        r.raise_for_status()
        at = r.json().get("token_info", {}).get("app_token")
        if not at:
            raise Exception("获取app_token失败")
        return at
    return with_retry(_do, op_name="获取应用令牌")

def process_data(step, tpl):
    today = time.strftime("%Y-%m-%d")
    fd = re.compile(r'.*?date%22%3A%22(.*?)%22%2C%22data.*?')
    fs = re.compile(r'.*?ttl%5C%22%3A(.*?)%2C%5C%22dis.*?')
    r = tpl
    dm = fd.match(r)
    if dm: r = r.replace(dm.group(1), today)
    sm = fs.match(r)
    if sm: r = r.replace(sm.group(1), str(step))
    return r

def send_data(uid, at, dj):
    def _do():
        url = "https://api-mifit-cn.huami.com/v1/data/band_data.json"
        hd = {"content-type": "application/x-www-form-urlencoded; charset=UTF-8","apptoken": at}
        p = "userid=" + uid + "&last_sync_data_time=1597306380&device_type=0&last_deviceid=DA932FFFFE8816E7&data_json=" + dj
        r = requests.post(url, data=p, headers=hd, timeout=15)
        r.raise_for_status()
        res = r.json()
        if "message" in res:
            return res
        raise Exception("上传失败")
    return with_retry(_do, op_name="上传数据")

def main():
    start = time.time()
    user = os.environ.get("MI_USER", "")
    pwd = os.environ.get("MI_PWD", "")
    ss = os.environ.get("STEP", "")
    if not user or not pwd:
        print("❌ 账号或密码未设置")
        return
    dt = ""
    if os.path.exists(DATA_FILE):
        with open(DATA_FILE, "r", encoding="utf-8") as f:
            dt = f.read().strip()
    if not dt:
        dt = os.environ.get("DATA_JSON", "")
    if not dt:
        print("❌ 缺少数据模板")
        return
    if not ss: ss = "18000-22000"
    pts = ss.split("-")
    step = random.randint(int(pts[0]), int(pts[1])) if len(pts) == 2 else int(ss)
    print("🏃 开始刷步数 | 手机: " + user[:3] + "****" + user[-4:] + " | 步数: " + str(step))
    try:
        code, tn = get_code(user, pwd)
        lt, uid = get_token(code, tn)
        at = get_apptoken(lt)
        dj = process_data(step, dt)
        res = send_data(uid, at, dj)
        el = round(time.time() - start, 2)
        if res.get("message") == "success":
            print("✅ 成功！步数已更新为 " + str(step) + " 步 | 耗时 " + str(el) + "秒")
        else:
            print("❌ 失败: " + json.dumps(res) + " | 耗时 " + str(el) + "秒")
    except Exception as e:
        el = round(time.time() - start, 2)
        print("❌ 错误: " + str(e) + " | 耗时 " + str(el) + "秒")

if __name__ == "__main__":
    main()
