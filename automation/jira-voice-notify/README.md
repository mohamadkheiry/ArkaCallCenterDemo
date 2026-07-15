# Jira Voice Notify (اطلاع‌رسانیِ صوتیِ Jira)

هر دقیقه Jira را چک می‌کند؛ وقتی ایشوی جدیدی به یک کاربر اساین شود، از طریق ترانکِ
Asterisk به موبایلِ او **زنگ می‌زند** و با **piper (صدای گنجی)** می‌گوید **چه کسی**
اساین کرده و **عنوانِ ایشو** را می‌خواند. یک **یادآورِ روزانه‌ی دیلی** هم دارد
(هر روز ساعت ۹:۲۹ به‌وقت تهران، به‌جز پنجشنبه و جمعه).

## اجزا
- `jira_notify.py` — ارکستریتور (بدونِ سکرت؛ از `config.json` می‌خواند).
- `config.example.json` — نمونه‌ی پیکربندی. `config.json` واقعی (شاملِ توکن‌ها)
  فقط روی سرور و با `chmod 600` نگه داشته می‌شود و **در گیت نیست**.

## استقرار روی سرور Isabel/Asterisk (Rocky 8)
1. **piper + صدای گنجی** (چون باینریِ piper به glibc جدید نیاز دارد، از Miniconda + pip استفاده می‌کنیم):
   ```bash
   # Miniconda (در صورت فیلترینگ از پروکسی استفاده کنید: curl -x http://user:pass@host:port ...)
   bash Miniconda3-latest-Linux-x86_64.sh -b -p /opt/miniconda
   /opt/miniconda/bin/conda create -y -n piper python=3.11
   /opt/miniconda/envs/piper/bin/pip install piper-tts
   # صدای گنجی
   mkdir -p /opt/arka-jira/voices
   curl -L -o /opt/arka-jira/voices/fa_IR-ganji-medium.onnx \
     https://huggingface.co/rhasspy/piper-voices/resolve/main/fa/fa_IR/ganji/medium/fa_IR-ganji-medium.onnx
   curl -L -o /opt/arka-jira/voices/fa_IR-ganji-medium.onnx.json \
     https://huggingface.co/rhasspy/piper-voices/resolve/main/fa/fa_IR/ganji/medium/fa_IR-ganji-medium.onnx.json
   ```
2. فایل‌ها:
   ```
   /opt/arka-jira/jira_notify.py
   /opt/arka-jira/config.json      # از روی config.example.json، chmod 600
   /opt/arka-jira/voices/...        # صدای گنجی
   /opt/arka-jira/state/            # وضعیت (خودکار ساخته می‌شود)
   ```
3. **کرون** (هر دقیقه، با flock برای جلوگیری از هم‌پوشانی):
   ```
   * * * * * /usr/bin/flock -n /tmp/arka-jira.lock /opt/miniconda/envs/piper/bin/python /opt/arka-jira/jira_notify.py >> /opt/arka-jira/cron.log 2>&1
   ```
   و `systemctl enable crond`.

## نکات
- **تماسِ خروجی**: از Outbound Route با پیش‌شماره‌ی `9` استفاده می‌شود
  (`Local/9<phone>@from-internal`) که CallerID و فرمتِ درست را می‌سازد.
- **قاعده‌ی ویرگول**: طبق نیازمندی، در همه‌ی TTSهای piper بین کلمات ویرگول گذاشته
  می‌شود (`comma_words`) برای تلفظِ بهترِ فارسی.
- **زمانِ تهران**: سرور در تایم‌زونِ دیگری است؛ زمانِ تهران به‌صورت `UTC+3:30`
  ثابت (بدون DST) محاسبه می‌شود.
- **اولین اجرا** = baseline: ایشوهای موجود «دیده‌شده» علامت می‌خورند و زنگی زده
  نمی‌شود؛ فقط اساین‌های جدیدِ بعد از آن اطلاع داده می‌شوند.
- تشخیصِ اساین‌کننده از `changelog` (آخرین تغییرِ فیلد assignee به آن کاربر).
