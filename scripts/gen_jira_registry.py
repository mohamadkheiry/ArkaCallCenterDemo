#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
تولیدِ خودکارِ «رجیستریِ ایشیوهای جیرا» (docs/06-jira-issues.md) از روی خودِ جیرا.

چرا: تا وقتی توسعه ادامه دارد، فهرستِ ایشیوها در گیت باید با جیرا هم‌گام بماند تا برای
ادامه‌ی کار یا پشتیبانی مرجعِ قابل‌اعتماد داشته باشیم. سند را دستی ویرایش نکنید؛
این اسکریپت را دوباره اجرا کنید.

اجرا:
    # ویندوز (PowerShell):  $env:JIRA_TOKEN="<PAT>"
    # لینوکس/مک:            export JIRA_TOKEN="<PAT>"
    python scripts/gen_jira_registry.py                # اپیکِ پیش‌فرض (AIP-1953)
    python scripts/gen_jira_registry.py AIP-2100       # اپیکِ دیگر

نکته: توکن فقط از متغیرِ محیطی خوانده می‌شود و هرگز در مخزن ذخیره نمی‌شود.
گواهیِ SSL جیرا self-signed است، پس راستی‌آزمایی غیرفعال می‌شود.
"""
import datetime, json, os, ssl, sys, urllib.parse, urllib.request
from collections import Counter

# کنسولِ ویندوز پیش‌فرض cp1252 است و چاپِ فارسی (یا مسیرِ فارسی) آن را می‌شکند.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

JIRA_BASE = os.environ.get("JIRA_BASE", "https://195.177.255.11").rstrip("/")
TOKEN = os.environ.get("JIRA_TOKEN")
EPIC = sys.argv[1] if len(sys.argv) > 1 else "AIP-1953"
BROWSE = JIRA_BASE + "/browse"

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "docs", "06-jira-issues.md"))

_CTX = ssl.create_default_context()
_CTX.check_hostname = False
_CTX.verify_mode = ssl.CERT_NONE

FA_MONTHS = ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
             "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"]


def to_fa_digits(s):
    return str(s).translate(str.maketrans("0123456789", "۰۱۲۳۴۵۶۷۸۹"))


def jalali(g_y, g_m, g_d):
    """تبدیلِ میلادی → جلالی (الگوریتمِ استاندارد)."""
    g_days_in_month = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
    gy, gm, gd = g_y - 1600, g_m - 1, g_d - 1
    g_day_no = 365 * gy + (gy + 3) // 4 - (gy + 99) // 100 + (gy + 399) // 400
    for i in range(gm):
        g_day_no += g_days_in_month[i]
    if gm > 1 and ((g_y % 4 == 0 and g_y % 100 != 0) or (g_y % 400 == 0)):
        g_day_no += 1
    g_day_no += gd
    j_day_no = g_day_no - 79
    j_np = j_day_no // 12053
    j_day_no %= 12053
    jy = 979 + 33 * j_np + 4 * (j_day_no // 1461)
    j_day_no %= 1461
    if j_day_no >= 366:
        jy += (j_day_no - 1) // 365
        j_day_no = (j_day_no - 1) % 365
    for i in range(11):
        jm_len = 31 if i < 6 else 30
        if j_day_no < jm_len:
            break
        j_day_no -= jm_len
    else:
        i = 11
    return jy, i + 1, j_day_no + 1


def jira_get(path):
    req = urllib.request.Request(JIRA_BASE + path, headers={
        "Authorization": "Bearer " + TOKEN,
        "Accept": "application/json",
    })
    with urllib.request.urlopen(req, timeout=40, context=_CTX) as r:
        return json.loads(r.read().decode("utf-8"))


def fetch(epic):
    jql = '("Epic Link" = %s OR key = %s) ORDER BY key ASC' % (epic, epic)
    q = urllib.parse.urlencode({"jql": jql, "maxResults": "300",
                                "fields": "summary,issuetype,status"})
    data = jira_get("/rest/api/2/search?" + q)
    return [{"key": i["key"],
             "type": i["fields"]["issuetype"]["name"],
             "status": i["fields"]["status"]["name"],
             "summary": i["fields"]["summary"]} for i in data.get("issues", [])]


def table(items):
    out = ["| کلید | وضعیت | عنوان |", "|------|:-----:|-------|"]
    for r in items:
        out.append("| [%s](%s/%s) | %s | %s |" % (r["key"], BROWSE, r["key"], r["status"], r["summary"]))
    return "\n".join(out)


def build_doc(epic, stories, tasks, others, total):
    n = datetime.date.today()
    jy, jm, jd = jalali(n.year, n.month, n.day)
    stamp = "‏%s %s %s (%s)" % (to_fa_digits(jd), FA_MONTHS[jm - 1], to_fa_digits(jy), to_fa_digits(n.isoformat()))
    statuses = Counter(r["status"] for r in ([epic] + stories + tasks + others))
    status_line = "، ".join("%s: %s" % (k, to_fa_digits(v)) for k, v in statuses.items())
    others_section = ""
    if others:
        others_section = "\n## سایر ایشیوها (%s)\n\n%s\n" % (to_fa_digits(len(others)), table(others))

    return f"""# رجیستریِ ایشیوهای جیرا — کال سنتر هوشمند آرکا

> این فایل، *نقشه‌ی ردیابی* (traceability) بین کارِ انجام‌شده و ایشیوهای جیراست.
> هدف: اگر بعداً خواستید **توسعه را ادامه دهید، اپیکِ جدید باز کنید، یا پشتیبانی کنید**، بدونِ گشتن در جیرا
> بدانید چه چیزی قبلاً انجام شده، کجاست، و چطور ایشیوهای مرتبط را بسازید.
>
> آخرین همگام‌سازی با جیرا: **{stamp}** — مجموع: {to_fa_digits(total)} ایشیو ({status_line}).
>
> ⚠️ این سند **خودکار تولید می‌شود**؛ دستی ویرایشش نکنید. برای به‌روزرسانی:
> `JIRA_TOKEN=<PAT> python scripts/gen_jira_registry.py {epic['key']}`

## ۱. مشخصاتِ پروژه در جیرا

| مورد | مقدار |
|------|-------|
| آدرس جیرا | `{JIRA_BASE}` |
| پروژه | **AIP** — «AI Products» (id: `11104`) |
| اپیکِ این فاز | [{epic['key']}]({BROWSE}/{epic['key']}) — **{epic['summary']}** (وضعیت: {epic['status']}) |
| نوع‌های ایشیو | `Task`, `Story`, `Bug`, `Epic`, `Tools` |
| نویسنده/کاربر | `mr.kheiry` (احراز هویت با PAT به‌صورت `Authorization: Bearer <token>`) |

### فیلدهای سفارشیِ لازم (Jira Data Center)
| فیلد | شناسه | کاربرد |
|------|-------|--------|
| Epic Name | `customfield_10104` | **الزامی** هنگام ساختِ یک Epic |
| Epic Link | `customfield_10108` | برای اتصالِ Story/Task به اپیک (مقدار = کلیدِ اپیک، مثلاً `{epic['key']}`) |
| Story Points | `customfield_10110` | اختیاری |
| Sprint | `customfield_10107` | اختیاری |

### مسیرِ گردشِ کار (Workflow)
ایشیوها از `Backlog` ساخته می‌شوند و **transition مستقیم به Done وجود ندارد**؛ باید گام‌به‌گام عبور کنند:

```
Backlog --(11 READY)--> READY --(21 In Progress)--> In Progress
        --(31 Test)--> Test --(61 Approved)--> Approved --(81 Done)--> Done
```
> شناسه‌های transition: `11` READY، `21` In Progress، `31` Test، `61` Approved، `81` Done (و `111` Pending از هر وضعیت).

## ۲. اپیک

{table([epic])}

## ۳. استوری‌ها — قابلیت‌های تحویل‌شده ({to_fa_digits(len(stories))})

{table(stories)}

## ۴. تسک‌ها — کارها، تست‌ها و مستندات ({to_fa_digits(len(tasks))})

{table(tasks)}
{others_section}
## ۵. مستنداتِ مرتبط (در همین مخزن)

| سند | توضیح |
|-----|-------|
| [01-architecture.md](01-architecture.md) | معماری سامانه + دیاگرام |
| [02-use-cases.md](02-use-cases.md) | بازیگران و موارد کاربردی |
| [03-activity-diagrams.md](03-activity-diagrams.md) | نمودارهای Activity |
| [04-data-model.md](04-data-model.md) | مدل داده (ERD) |
| [05-deployment.md](05-deployment.md) | راهنمای استقرار و عملیات |

## ۶. چطور توسعه را ادامه دهیم؟

### الف) ادامه‌ی کار
اگر اپیکِ بالا بسته (Done) شده است، برای کارِ جدید:
1. یک **اپیکِ جدید** برای فازِ بعد بسازید (بند «ب») و ایشیوها را ذیلِ آن ببرید.
2. یا اگر کارِ کوچکی است، یک `Story`/`Task` بسازید و با `customfield_10108` به اپیکِ مناسب وصل کنید.
3. برای **بازکردنِ دوباره‌ی** یک ایشیوی بسته، از transition ‏`111` (Pending) یا مسیرِ workflow استفاده کنید.

### ب) ساختِ اپیکِ جدید (نمونه‌ی REST)
```bash
curl -k -X POST "{JIRA_BASE}/rest/api/2/issue" \\
  -H "Authorization: Bearer <PAT>" -H "Content-Type: application/json" \\
  -d '{{
    "fields": {{
      "project": {{"key": "AIP"}},
      "issuetype": {{"name": "Epic"}},
      "summary": "فاز بعدی — ...",
      "customfield_10104": "فاز بعدی — ...",
      "description": "..."
    }}
  }}'
```

### ج) ساختِ Story/Task ذیلِ اپیک
```bash
curl -k -X POST "{JIRA_BASE}/rest/api/2/issue" \\
  -H "Authorization: Bearer <PAT>" -H "Content-Type: application/json" \\
  -d '{{
    "fields": {{
      "project": {{"key": "AIP"}},
      "issuetype": {{"name": "Story"}},
      "summary": "عنوان",
      "description": "شرح با Jira wiki markup",
      "customfield_10108": "{epic['key']}"
    }}
  }}'
```

### د) یافتنِ ایشیوهای یک اپیک (JQL)
```
"Epic Link" = {epic['key']} ORDER BY key ASC
```

### هـ) نکاتِ عملیاتی برای پشتیبانی
* گواهیِ SSL جیرا self-signed است → در کلاینت‌ها راستی‌آزماییِ گواهی را غیرفعال کنید (`curl -k` / `verify=False`).
* اسرار (PAT جیرا، کلید OpenAI، رمزِ ایزابل) **هرگز** در مخزن قرار نگیرند.
* برای کارِ روی سرور ایزابل از **paramiko** استفاده کنید (نه plink) — به [05-deployment.md](05-deployment.md) مراجعه کنید.
* اتوماسیونِ اطلاع‌رسانیِ صوتیِ جیرا روی ایزابل در `/opt/arka-jira/` مستقر است (پیکربندی: `config.json`).
  ساختنِ انبوهِ ایشیو دیگر سیلِ تماس ایجاد نمی‌کند (ادغام + بازه‌ی خنک‌شدن — به README همان پوشه مراجعه کنید).
"""


def main():
    if not TOKEN:
        sys.exit("خطا: متغیرِ محیطیِ JIRA_TOKEN تنظیم نشده است.")
    rows = fetch(EPIC)
    if not rows:
        sys.exit("هیچ ایشیویی برای %s پیدا نشد." % EPIC)
    epics = [r for r in rows if r["type"] == "Epic"]
    if not epics:
        sys.exit("خودِ اپیک %s در نتایج نبود." % EPIC)

    epic = epics[0]
    kn = lambda x: int(x["key"].split("-")[1])
    stories = sorted([r for r in rows if r["type"] == "Story"], key=kn)
    tasks = sorted([r for r in rows if r["type"] == "Task"], key=kn)
    others = sorted([r for r in rows if r["type"] not in ("Epic", "Story", "Task")], key=kn)

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(build_doc(epic, stories, tasks, others, len(rows)))

    print("OK: %s" % OUT)
    print("epic=%s | stories=%d tasks=%d others=%d total=%d"
          % (epic["key"], len(stories), len(tasks), len(others), len(rows)))


if __name__ == "__main__":
    main()
