#!/opt/miniconda/envs/piper/bin/python
# -*- coding: utf-8 -*-
"""
آرکا — نوتیفایِ Jira با تماس صوتی (piper / صدای گنجی).
هر دقیقه اجرا می‌شود:
  • برای هر کاربر، ایشوهای تازه‌اساین‌شده را از Jira می‌خواند و زنگ می‌زند
    (می‌گوید چه کسی اساین کرده و عنوان ایشو را می‌خواند).
  • یادآورِ روزانه‌ی دیلی: ساعت ۹:۲۹ به‌وقت تهران، به‌جز پنجشنبه و جمعه.
همه‌ی TTSها با piper و «ویرگول بین کلمات» تولید می‌شوند.
"""
import json, os, sys, ssl, time, hashlib, subprocess, datetime
import urllib.request, urllib.parse

# کرون PATH محدودی دارد؛ مسیرهای asterisk(/usr/sbin) و sox/chown را تضمین کن.
os.environ["PATH"] = "/usr/sbin:/usr/bin:/bin:/sbin:" + os.environ.get("PATH", "")

BASE      = "/opt/arka-jira"
CONF      = os.path.join(BASE, "config.json")
STATE_DIR = os.path.join(BASE, "state")
SND_TMP   = os.path.join(BASE, "sounds")
AST_SND   = "/var/lib/asterisk/sounds/arka"          # asterisk sounds (Playback: arka/<name>)
PIPER_PY  = "/opt/miniconda/envs/piper/bin/python"
VOICE     = os.path.join(BASE, "voices/fa_IR-ganji-medium.onnx")
LOG       = os.path.join(BASE, "notify.log")

os.makedirs(STATE_DIR, exist_ok=True); os.makedirs(SND_TMP, exist_ok=True)
os.makedirs(AST_SND, exist_ok=True)

def log(m):
    line = "%s %s" % (datetime.datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S"), m)
    print(line)
    try:
        with open(LOG, "a", encoding="utf-8") as f: f.write(line + "\n")
    except Exception: pass

def load(p, default):
    try:
        with open(p, encoding="utf-8") as f: return json.load(f)
    except Exception: return default

def save(p, data):
    tmp = p + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f: json.dump(data, f, ensure_ascii=False)
    os.replace(tmp, p)

# ---------- Jira ----------
_SSL = ssl.create_default_context(); _SSL.check_hostname = False; _SSL.verify_mode = ssl.CERT_NONE

def jira_get(base, token, path):
    url = base.rstrip("/") + path
    req = urllib.request.Request(url, headers={"Authorization": "Bearer " + token,
                                               "Accept": "application/json"})
    with urllib.request.urlopen(req, timeout=25, context=_SSL) as r:
        return json.loads(r.read().decode("utf-8"))

def recent_assignments(base, token, jira_user, jira_key):
    """لیستِ رویدادهای «اساین‌شدن به این کاربر» در ایشوهای اخیراً به‌روزشده.
       هر رویداد: (event_id, assigner_name, issue_key, summary, created_epoch)."""
    jql = 'assignee = "%s" AND updated >= "-30m" ORDER BY updated DESC' % jira_user
    q = urllib.parse.urlencode({"jql": jql, "maxResults": "25", "expand": "changelog",
                                "fields": "summary,created,reporter,creator,assignee"})
    data = jira_get(base, token, "/rest/api/2/search?" + q)
    out = []
    for it in data.get("issues", []):
        key = it["key"]; f = it.get("fields", {})
        summary = f.get("summary") or key
        ev = None
        # آخرین تغییرِ assignee به این کاربر در changelog
        for h in it.get("changelog", {}).get("histories", []):
            for item in h.get("items", []):
                if item.get("field") == "assignee" and (item.get("to") == jira_key):
                    author = (h.get("author") or {}).get("displayName") or (h.get("author") or {}).get("name") or "نامشخص"
                    ev = {"eid": "%s#%s" % (key, h.get("id")), "assigner": author,
                          "key": key, "summary": summary, "ts": _parse_ts(h.get("created"))}
        if ev is None:
            # اساین در زمان ساخت (بدون رکورد در changelog) → اساین‌کننده = گزارش‌گر/سازنده
            rep = (f.get("reporter") or f.get("creator") or {})
            author = rep.get("displayName") or rep.get("name") or "نامشخص"
            ev = {"eid": "%s#created" % key, "assigner": author, "key": key,
                  "summary": summary, "ts": _parse_ts(f.get("created"))}
        out.append(ev)
    return out

def _parse_ts(s):
    if not s: return 0
    try:
        # 2026-07-15T09:04:03.000+0000
        s2 = s[:19]
        return time.mktime(time.strptime(s2, "%Y-%m-%dT%H:%M:%S"))
    except Exception:
        return time.time()

# ---------- TTS (piper / گنجی) با ویرگول بین کلمات ----------
def comma_words(text):
    words = [w for w in str(text).replace("\n", " ").split(" ") if w.strip()]
    return " , ".join(words)

def tts(text, out_basename):
    """متن → WAV 8k مونو در پوشه‌ی sounds آسترسک. نام فایل asterisk: arka/<out_basename>"""
    raw = os.path.join(SND_TMP, out_basename + "_raw.wav")
    final = os.path.join(AST_SND, out_basename + ".wav")
    piped = comma_words(text)
    p = subprocess.run([PIPER_PY, "-m", "piper", "--model", VOICE, "--output_file", raw],
                       input=piped.encode("utf-8"),
                       stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=120)
    if p.returncode != 0 or not os.path.exists(raw):
        log("TTS FAIL: " + p.stderr.decode("utf-8", "replace")[:200]); return None
    subprocess.run(["sox", raw, "-r", "8000", "-c", "1", "-b", "16", final],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run(["chown", "asterisk:asterisk", final], stderr=subprocess.DEVNULL)
    try: os.remove(raw)
    except Exception: pass
    return "arka/" + out_basename if os.path.exists(final) else None

# ---------- تماس خروجی (مسیرِ اثبات‌شده: 9 + شماره) ----------
def call(phone, sound_ref):
    dial = "9" + phone   # outbound route prefix
    cmd = ('channel originate Local/%s@from-internal application Playback %s'
           % (dial, sound_ref))
    r = subprocess.run(["asterisk", "-rx", cmd], stdout=subprocess.PIPE,
                       stderr=subprocess.STDOUT, timeout=30)
    log("CALL %s play=%s -> %s" % (phone, sound_ref, r.stdout.decode('utf-8','replace').strip()[:120]))

# ---------- منطق اصلی ----------
def check_user(u):
    st_path = os.path.join(STATE_DIR, "user_%s.json" % u["name"])
    st = load(st_path, {"seen": [], "baselined": False})
    seen = set(st.get("seen", []))
    try:
        events = recent_assignments(u["jira_base"], u["token"], u["jira_user"], u["jira_key"])
    except Exception as e:
        log("JIRA ERR %s: %s" % (u["name"], e)); return
    now = time.time()
    if not st.get("baselined"):
        # اولین اجرا: همه‌ی رویدادهای فعلی را دیده‌شده علامت بزن، زنگ نزن
        for ev in events: seen.add(ev["eid"])
        save(st_path, {"seen": list(seen)[-500:], "baselined": True})
        log("baselined %s (%d existing)" % (u["name"], len(events))); return
    new = [ev for ev in events if ev["eid"] not in seen and (now - ev["ts"]) < 1800]
    new.sort(key=lambda e: e["ts"])
    for ev in new:
        text = ("سلام، یک ایشوی جدید به شما اساین شد. اساین‌کننده: %s. عنوان ایشو: %s"
                % (ev["assigner"], ev["summary"]))
        name = "jira_%s_%s" % (u["name"], hashlib.md5(ev["eid"].encode()).hexdigest()[:8])
        ref = tts(text, name)
        if ref:
            call(u["phone"], ref)
            log("NOTIFIED %s issue=%s by=%s" % (u["name"], ev["key"], ev["assigner"]))
        seen.add(ev["eid"])
        time.sleep(2)
    # همه‌ی رویدادهای فعلی را هم دیده‌شده کن (تا دوباره زنگ نزند)
    for ev in events: seen.add(ev["eid"])
    save(st_path, {"seen": list(seen)[-500:], "baselined": True})

def tehran_now():
    return datetime.datetime.utcnow() + datetime.timedelta(hours=3, minutes=30)

def daily_reminder(cfg):
    d = cfg.get("daily_reminder")
    if not d: return
    tn = tehran_now()
    if tn.weekday() in d.get("skip_weekdays", [3, 4]): return   # پنجشنبه=3، جمعه=4
    if not (tn.hour == d["hour"] and tn.minute == d["minute"]): return
    st_path = os.path.join(STATE_DIR, "daily.json")
    st = load(st_path, {"last": ""})
    today = tn.strftime("%Y-%m-%d")
    if st.get("last") == today: return   # امروز فرستاده شده
    ref = tts(d["text"], "daily_reminder_%s" % today.replace("-", ""))
    if ref:
        call(d["phone"], ref)
        log("DAILY reminder sent to %s" % d["phone"])
    save(st_path, {"last": today})

def main():
    cfg = load(CONF, None)
    if not cfg: log("no config"); return
    for u in cfg["users"]:
        u["jira_base"] = cfg["jira_base"]
        try: check_user(u)
        except Exception as e: log("user %s err: %s" % (u.get("name"), e))
    try: daily_reminder(cfg)
    except Exception as e: log("daily err: %s" % e)

if __name__ == "__main__":
    main()
