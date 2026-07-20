# راهنمای استقرار — Arka Call Center

راهنمای کامل بالا آوردن کل سامانه (دیتابیس + API + worker تلفنی + داشبورد) با Docker.

> **چندسکویی:** همه‌ی ایمیج‌ها Linux-based هستند (dotnet، node، nginx، mysql)، پس این استک
> عیناً روی **Ubuntu / هر لینوکسی** و همچنین **ویندوز** (Docker Desktop) اجرا می‌شود. هیچ وابستگی
> به ویندوز وجود ندارد.

فهرست:
1. [معماری استقرار](#۱-معماری-استقرار)
2. [پیش‌نیازها](#۲-پیشنیازها)
3. [راه سریع روی Ubuntu (اسکریپت)](#۳-راه-سریع-روی-ubuntu-اسکریپت)
4. [راه دستی (هر سیستم‌عامل)](#۴-راه-دستی-هر-سیستمعامل)
5. [متغیرهای محیطی (env.)](#۵-متغیرهای-محیطی-env)
6. [پس از استقرار: تنظیمات ضروری](#۶-پس-از-استقرار-تنظیمات-ضروری)
7. [اتصال به ایزابل (تلفن)](#۷-اتصال-به-ایزابل-تلفن)
8. [دستورهای پرکاربرد](#۸-دستورهای-پرکاربرد)
9. [عیب‌یابی](#۹-عیبیابی)
10. [به‌روزرسانی و پشتیبان‌گیری](#۱۰-بهروزرسانی-و-پشتیبانگیری)

---

## ۱. معماری استقرار

چهار سرویس در `docker-compose.yml`:

| سرویس | نقش | پورت میزبان |
|-------|-----|-------------|
| `db` | MySQL 8 (volume: `db_data`) | داخلی |
| `api` | .NET 9 Web API (migration + seed خودکار در استارت) | `8080` |
| `realtime` | worker پل صوتی (AudioSocket ⇄ OpenAI realtime) | `9092` |
| `web` | nginx: داشبورد React + پراکسی `/api` | `8081` |

`api` و `realtime` یک volume مشترک `uploads` دارند (فایل‌های صوتی/تصویری).

---

## ۲. پیش‌نیازها

- Docker Engine + افزونه‌ی Docker Compose v2
- دسترسی به اینترنت برای pull ایمیج‌ها و restore پکیج‌ها
- پورت‌های آزاد: `8081` (وب)، `8080` (API)، `9092` (AudioSocket تلفن)

نصب Docker روی Ubuntu:
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER" && newgrp docker   # تا بدون sudo کار کند
docker --version && docker compose version
```

---

## ۳. راه سریع روی Ubuntu (اسکریپت)

```bash
# دریافت پروژه — یکی از دو راه:
#   الف) از زیپِ آماده:
unzip ArkaCallCenter-deploy.zip && cd ArkaCallCenterDemo
#   ب) یا clone:
# git clone https://github.com/mohamadkheiry/ArkaCallCenterDemo.git && cd ArkaCallCenterDemo

chmod +x deploy.sh && ./deploy.sh
```

`deploy.sh` به‌صورت خودکار: Docker را چک می‌کند، `.env` را از روی `.env.deploy.example` می‌سازد
(اگر نبود)، و کل استک را `build` و `up -d` می‌کند و آدرس‌ها را چاپ می‌کند.

زیپِ آماده‌ی استقرار در ریپو: [`release/ArkaCallCenter-deploy.zip`](release/ArkaCallCenter-deploy.zip)
(سورس + compose + Dockerfileها + `deploy.sh`، بدون `node_modules`/`bin` و بدون هیچ رمز).

---

## ۴. راه دستی (هر سیستم‌عامل)

```bash
cd ArkaCallCenterDemo

# ۱) تنظیم متغیرها
cp .env.deploy.example .env
nano .env        # مقادیر را تنظیم کنید (بخش ۵)

# ۲) ساخت و اجرا
docker compose up -d --build

# ۳) وضعیت و لاگ
docker compose ps
docker compose logs -f api    # migration خودکار + seed
```

پس از بالا آمدن:

| مورد | آدرس |
|------|------|
| 🖥️ داشبورد | `http://SERVER_IP:8081` |
| 🔌 API / سلامت | `http://SERVER_IP:8080/health` |
| 📘 Swagger | `http://SERVER_IP:8080/swagger` |
| 📗 Scalar | `http://SERVER_IP:8080/scalar` |
| ☎️ AudioSocket تلفن | پورت TCP `9092` |

> Swagger/Scalar فقط وقتی `ASPNETCORE_ENVIRONMENT=Development` باشد فعال‌اند.
> باندل Scalar از CDN می‌آید (مرورگر باید اینترنت داشته باشد)؛ Swagger کاملاً آفلاین است.

---

## ۵. متغیرهای محیطی (`.env`)

فایل `.env` کنار `docker-compose.yml` (نمونه: `.env.deploy.example`). Compose خودکار آن را می‌خواند.

| متغیر | توضیح |
|-------|-------|
| `MYSQL_ROOT_PASSWORD` | رمز روت MySQL داخل کانتینر |
| `JWT_SECRET` | کلید امضای JWT (حداقل ۳۲ کاراکتر) |
| `SUPERADMIN_PHONE` | شماره‌ای که در seed اولیه سوپرادمین می‌شود (مثلاً `09015909044`) |
| `API_PORT` / `WEB_PORT` / `AUDIOSOCKET_PORT` | پورت‌های میزبان (پیش‌فرض 8080/8081/9092) |
| `CALL_IDLE_TIMEOUT_SECONDS` | قطع خودکار تماس پس از سکوت کامل کاربر و دستیار؛ پیش‌فرض `60`، مقدار `0` یعنی غیرفعال |
| `OPENAI_REALTIME_MODEL` | مدل مکالمه؛ مقدار پیشنهادی فعلی `gpt-realtime-2.1` |
| `OPENAI_TRANSCRIPTION_MODEL` | مدل تبدیل گفتار تماس‌گیرنده؛ برای فارسی `gpt-4o-transcribe` |
| `TRANSCRIPTION_LANGUAGE` | راهنمای زبان تبدیل گفتار؛ برای فارسی `fa` |
| `DEFAULT_VOICE` | گوینده پیش‌فرض؛ برای کیفیت عمومی بهتر `marin` (گزینه جایگزین `cedar`) |
| `FRONTEND_ORIGIN` | origin وب برای CORS (مثلاً `http://SERVER_IP:8081`) |
| `ASPNETCORE_ENVIRONMENT` | `Development` (Swagger/Scalar روشن) یا `Production` |
| `ASTERISK_HOST` / `ASTERISK_SSH_USER` / `ASTERISK_SSH_PASSWORD` | برای provisioning خودکار داخلی و آپلود پیام پذیرش روی ایزابل (اختیاری) |

> کلیدهای حساس (OpenAI و SMS.ir) را **از پنل سوپرادمین** وارد کنید تا در دیتابیس امن (ماسک‌شده)
> ذخیره شوند؛ نیازی به گذاشتن آن‌ها در `.env` نیست. `.env` هرگز در گیت قرار نمی‌گیرد.

---

## ۶. پس از استقرار: تنظیمات ضروری

1. با `SUPERADMIN_PHONE` وارد داشبورد شوید.
   - در حالت توسعه، کد ورود در لاگ چاپ می‌شود: `docker compose logs api | grep SMS`
   - اگر SMS.ir تنظیم شده باشد، کد واقعی پیامک می‌شود.
2. **پنل سوپرادمین → OpenAI و RAG:** `Base URL` و `API Key` اوپن‌ای‌آی را وارد کنید (برای پاسخ AI، تولید صوت پیام‌ها و نمونه‌صدا).
3. **پنل سوپرادمین → SMS.ir:** `API Key`، `شناسه قالب کد تأیید (Template ID)` و در صورت نیاز `شماره خط` را وارد کنید.
4. **پنل سوپرادمین → پیام پیش‌فرض / پذیرش و انتظار / گوینده‌ها:** متن‌ها و صوت‌ها را تنظیم و تولید کنید.

پیام خوش‌آمد جدید به‌صورت WAV در volume مشترک `uploads` ذخیره می‌شود و worker فایل‌های داخلی‌های فعال را هنگام startup در حافظه گرم می‌کند؛ بنابراین شروع مکالمه منتظر تولید آنلاین صدا یا handshake مدل نمی‌ماند. WAVهای streaming با اندازهٔ نامشخص نیز پشتیبانی می‌شوند. پس از ارتقای نسخه‌ای قدیمی که فایل خوش‌آمد MP3 دارد، پیام خوش‌آمد را یک بار در پنل ذخیره کنید تا کش WAV با گوینده فعلی بازتولید شود.

---

## ۷. اتصال به ایزابل (تلفن)

- سرویس `realtime` روی پورت `9092` گوش می‌دهد.
- فایل‌های `telephony/extensions_arka.conf` و `telephony/pjsip_custom.conf` را روی ایزابل نصب کنید و
  `ARKA_WORKER_HOST` را برابر IP سرورِ این استک قرار دهید.
- ماژول `app_audiosocket` باید فعال باشد. راهنمای کامل: [`telephony/README.md`](telephony/README.md).

---

## ۸. دستورهای پرکاربرد

```bash
docker compose ps                       # وضعیت سرویس‌ها
docker compose logs -f api              # لاگ زنده‌ی API
docker compose logs -f realtime         # لاگ worker تلفنی
docker compose restart api              # ری‌استارت یک سرویس
docker compose down                     # توقف (داده‌ها می‌مانند)
docker compose up -d --build            # اعمال تغییرات و اجرای مجدد
```

> اگر بعد از تغییر کد، کانتینر جایگزین نشد، صریح بازسازی کنید:
> `docker compose build <svc> && docker compose up -d --force-recreate --no-deps <svc>`

سنجش مسیر AudioSocket و زمان اولین صدای داخلی، بدون تماس بیرونی:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-audiosocket.ps1 -Extension 2 -DurationSeconds 4
```

خروجی `firstAudibleMs` باید مثبت و ترجیحاً کمتر از `1000` باشد. اسکریپت به Secret یا شمارهٔ واقعی نیاز ندارد.

---

## ۹. عیب‌یابی

| مشکل | راه‌حل |
|------|--------|
| `db is unhealthy` | لاگ db را ببینید؛ مطمئن شوید `command` نامعتبری در compose نیست و volume سالم است. |
| Swagger/Scalar باز نمی‌شود | `ASPNETCORE_ENVIRONMENT=Development` باشد. |
| از دستگاه دیگر LAN باز نمی‌شود | فایروال سرور پورت‌های ۸۰۸۰/۸۰۸۱/۹۰۹۲ را باز کند. |
| پاسخ AI کار نمی‌کند | کلید OpenAI در پنل ثبت شده؟ لاگ `realtime` و `api` را ببینید. |
| ابتدای تماس سکوت طولانی دارد | پیام خوش‌آمد را یک بار ذخیره کنید تا WAV ساخته شود؛ سپس اسکریپت AudioSocket بالا و لاگ `First greeting audio` را بررسی کنید. |
| سؤال نامرتبط جواب یکی از FAQها را می‌گیرد | ایندکس KB را بررسی کنید و مطمئن شوید نسخهٔ جدید `realtime` منتشر شده است؛ این نسخه پیش از هر پاسخ RAG را اجرا و در نبود نتیجه fallback پخش می‌کند. |
| داخلی تک‌رقمی از IVR قطع می‌شود | در context `arka-ai` الگوی `_X!` لازم است؛ `_X.` داخلی تک‌رقمی را match نمی‌کند. سپس `dialplan reload` اجرا شود. |
| پیامک ارسال نمی‌شود | کلید/قالب SMS.ir در پنل درست است؟ لاگ `api` پاسخ SMS.ir را نشان می‌دهد. |

---

## ۱۰. به‌روزرسانی و پشتیبان‌گیری

```bash
# به‌روزرسانی کد
git pull            # یا زیپ جدید را باز کنید
docker compose up -d --build

# پشتیبان‌گیری دیتابیس
docker compose exec db mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" arka_callcenter > backup.sql

# بازیابی
docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" arka_callcenter < backup.sql
```

> فایل‌های صوتی/تصویری در volume `uploads` و داده‌ها در volume `db_data` نگهداری می‌شوند؛
> `docker compose down` آن‌ها را حذف نمی‌کند، اما `docker compose down -v` **حذف می‌کند** (احتیاط).

---

## ۱۱. تحویل آفلاین v4 و محیط تست شبکه داخلی

در ۲۰ ژوئیهٔ ۲۰۲۶ نسخهٔ فعلی روی Windows/Docker Desktop با همان volumeهای
دیتابیس و uploads اجرا و از یک میزبان دیگر داخل شبکه تست شد:

- داشبورد داخلی: `http://192.168.10.175:8081`
- health داخلی: `http://192.168.10.175:8081/health`
- صفحهٔ دانلود بسته: `http://192.168.10.175:8090/`

بستهٔ `ArkaCallCenter-linux-handoff-20260720-v4.zip` برای Linux/amd64 شامل
ایمیج‌ها، دیتابیس و uploads فعلی است. SHA-256 فایل ZIP:

```text
f2927c4f426da01878d6c86d2b9b40fc79eda2aac62edde46461155a830d31ea
```

انتشار production در Jira Task `AIP-2072` زیر Epic `AIP-1953` ثبت و به
`A.gholami` واگذار شده است. دامنهٔ نهایی باید
`https://callcenterdemo.arkadp.com` باشد. جزئیات کامل مسیرهای محلی، reverse
proxy، TLS، امنیت و معیار پذیرش در
[`docs/08-windows-lan-and-v4-publish-handoff.md`](docs/08-windows-lan-and-v4-publish-handoff.md)
ثبت شده است.
