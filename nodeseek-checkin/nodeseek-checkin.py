import os
import time
import random
from datetime import datetime, timedelta, timezone
from curl_cffi import requests

from notify import send

# -------------------------- 环境变量配置 --------------------------
NS_COOKIE = os.getenv("NODESEEK_COOKIE", "")
PROXY_URL = os.getenv("NODESEEK_PROXY_URL")
SIGN_RANDOM = os.getenv("NODESEEK_SIGN_RANDOM", "true").lower() == "true"
RANDOM_DELAY_MIN = int(os.getenv("NODESEEK_RANDOM_DELAY_MIN", "0"))
RANDOM_DELAY_MAX = int(os.getenv("NODESEEK_RANDOM_DELAY_MAX", "0"))
FIXED_DELAY = int(os.getenv("NODESEEK_FIXED_DELAY", "0"))
SITE_DOMAIN = os.getenv("SITE_DOMAIN", "www.nodeseek.com")

BASE_URL = f"https://{SITE_DOMAIN}"

proxies = {"http": PROXY_URL, "https": PROXY_URL} if PROXY_URL else None

HEADERS = {
    'User-Agent': "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
    'sec-ch-ua': "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"",
    'sec-ch-ua-mobile': "?0",
    'sec-ch-ua-platform': "\"macOS\"",
    'sec-fetch-dest': "empty",
    'sec-fetch-mode': "cors",
    'sec-fetch-site': "same-origin",
    'referer': f"{BASE_URL}/board",
    'accept-language': "zh-CN,zh;q=0.9,en;q=0.8",
    'Content-Type': "application/json",
    'accept': "application/json, text/plain, */*",
    'origin': BASE_URL,
    'Cookie': NS_COOKIE
}

def sign_in():
    url = f"{BASE_URL}/api/attendance?random={'true' if SIGN_RANDOM else 'false'}"
    print(f"📡 请求: {url}")
    
    resp = requests.post(url, headers=HEADERS, impersonate="chrome110", proxies=proxies, timeout=30)
    print(f"📥 状态码: {resp.status_code}")
    
    if resp.status_code == 403:
        print(f"📋 响应: {resp.text[:300]}")
        return 'forbidden', "请求被拦截"
    
    data = resp.json()
    print(f"📋 响应: {data}")
    msg = data.get('message', '')
    
    if data.get("success") or "鸡腿" in msg:
        return 'success', msg
    elif "已完成签到" in msg:
        return 'already', msg
    return 'fail', msg

def get_signin_stats(days=30):
    """查询签到收益统计"""
    all_records = []
    stop_time = datetime.now(timezone.utc) - timedelta(days=days)
    
    for page in range(1, 11):
        try:
            resp = requests.get(
                f"{BASE_URL}/api/account/credit/page-{page}",
                headers=HEADERS, impersonate="chrome110", proxies=proxies, timeout=30
            )
            data = resp.json()
            if not data.get("success") or not data.get("data"):
                break
            records = data["data"]
            if not records:
                break
            all_records.extend(records)
            
            # 检查最后一条记录是否超出时间范围
            last_time = datetime.fromisoformat(records[-1][3].replace('Z', '+00:00'))
            if last_time < stop_time:
                break
            time.sleep(0.5)
        except:
            break
    
    # 严格按日期筛选签到收益记录
    signin_records = []
    for record in all_records:
        amount, balance, description, timestamp = record
        record_time = datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
        # 只统计指定天数内的签到收益
        if record_time >= stop_time and "签到收益" in description and "鸡腿" in description:
            signin_records.append(amount)
    
    if not signin_records:
        return None
    
    total = sum(signin_records)
    return {
        'total': total, 
        'average': round(total / len(signin_records), 2), 
        'days': len(signin_records)
    }

if __name__ == "__main__":
    notify_title = "NodeSeek签到通知"
    
    try:
        if not NS_COOKIE:
            print("❌ 请配置 NODESEEK_COOKIE")
            send(notify_title, "❌ 请配置 NODESEEK_COOKIE")
            exit(1)

        # 延迟
        delay = 0
        if FIXED_DELAY > 0:
            delay = FIXED_DELAY
            print(f"⏳ 固定延迟 {delay} 秒...")
        elif RANDOM_DELAY_MAX > 0:
            delay = random.randint(RANDOM_DELAY_MIN, RANDOM_DELAY_MAX)
            print(f"⏳ 随机延迟 {delay} 秒 (范围: {RANDOM_DELAY_MIN}-{RANDOM_DELAY_MAX})...")
        
        if delay > 0:
            time.sleep(delay)

        print(f"🎯 开始签到... (随机鸡腿: {'是' if SIGN_RANDOM else '否'})")
        result, msg = sign_in()
        
        if result == 'success':
            success_msg = f"🎉 签到成功: {msg}"
            print(success_msg)
            stats = get_signin_stats(30)
            if stats:
                stats_msg = f"📊 近30天签到{stats['days']}天，共{stats['total']}鸡腿，平均{stats['average']}/天"
                print(stats_msg)
                send(notify_title, f"{success_msg}\n{stats_msg}")
            else:
                send(notify_title, success_msg)
        elif result == 'already':
            print(f"⚠️ {msg}")
            send(notify_title, f"⚠️ 今日已签到: {msg}")
        elif result == 'forbidden':
            print(f"❌ {msg}")
            send(notify_title, f"❌ {msg}\n💡 请更新完整 Cookie（需含 session, cf_clearance）")
        else:
            print(f"❌ 失败: {msg}")
            send(notify_title, f"❌ 签到失败: {msg}")

    except Exception as e:
        print(f"❌ 错误: {e}")
        send(notify_title, f"❌ 错误: {e}")
        exit(1)