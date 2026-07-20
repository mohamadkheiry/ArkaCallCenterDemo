# اجرای داخلی ویندوز و تحویل انتشار v4

این سند وضعیت تحویل تاریخ ۲۰ ژوئیهٔ ۲۰۲۶ را ثبت می‌کند. هدف، ارائهٔ نسخهٔ قابل
تست در شبکهٔ داخلی شرکت و تحویل یک بستهٔ آفلاین کامل به DevOps برای انتشار روی
دامنهٔ نهایی است.

## سرویس داخلی فعال

- داشبورد: `http://192.168.10.175:8081`
- Health check: `http://192.168.10.175:8081/health`
- صفحهٔ دانلود DevOps: `http://192.168.10.175:8090/`
- استک برنامه: `db`، `api`، `realtime` و `web`
- سرویس فایل: کانتینر `arkacallcenter-artifacts` با restart policy برابر
  `unless-stopped`

داشبورد و صفحهٔ دانلود از میزبان مستقل `192.168.10.101` داخل LAN تست شدند و
هر دو پاسخ HTTP 200 گرفتند. فایل ZIP نیز با Content-Type برابر
`application/zip` و اندازهٔ صحیح پاسخ داده شد.

## بستهٔ تحویل

- پوشهٔ منبع بسته:
  `C:\Users\arka\Documents\projects\ArkaCallCenter-linux-handoff-20260720-v4`
- پوشهٔ قابل ارسال/دانلود:
  `C:\Users\arka\Documents\projects\ArkaCallCenter-DevOps-Downloads-20260720`
- فایل ZIP: `ArkaCallCenter-linux-handoff-20260720-v4.zip`
- اندازه: `385602620` بایت
- SHA-256:
  `f2927c4f426da01878d6c86d2b9b40fc79eda2aac62edde46461155a830d31ea`
- سورس build برنامه: شاخهٔ `main`، commit `c435139`

بسته شامل ایمیج‌های Linux/amd64 مربوط به API، Realtime، Web و MySQL 8.4،
snapshot دیتابیس فعلی، snapshot فایل‌های uploads، compose مستقل، نمونهٔ env،
checksumهای داخلی و راهنمای کامل فارسی است.

## Jira و مالک انتشار

- Task: [`AIP-2072`](https://195.177.255.11/browse/AIP-2072)
- Epic Link: `AIP-1953`
- Assignee: `A.gholami`
- وضعیت زمان تحویل: `READY`
- دامنهٔ مقصد: `https://callcenterdemo.arkadp.com`

شرح Task شامل لینک مستقیم بسته داخل LAN، checksum، مراحل `docker load` و
`docker compose up -d`، تنظیم Nginx، DNS، TLS، فایروال، health check، معیارهای
پذیرش و rollback است.

## الزامات امنیتی انتشار

- Secret، رمز، PAT و کلید سرویس داخل Git، Jira و بستهٔ env قرار نمی‌گیرد.
- `MYSQL_ROOT_PASSWORD`، `JWT_SECRET`، شمارهٔ Super Admin و
  `VOICE_SERVICE_SECRET` فقط از کانال امن تحویل می‌شوند.
- `FRONTEND_ORIGIN` باید دقیقاً
  `https://callcenterdemo.arkadp.com` باشد.
- Web و API به‌صورت پیش‌فرض فقط روی `127.0.0.1` bind می‌شوند و Nginx روی
  80/443 جلوی آن‌ها قرار می‌گیرد.
- پورت AudioSocket فقط از IP سرور Asterisk مجاز است.
- اجرای `docker compose down -v` ممنوع است، چون داده‌ها و uploads را حذف می‌کند.

## معیار پذیرش DevOps

1. DNS دامنه به IP عمومی سرور اشاره کند.
2. TLS معتبر فعال و redirect از HTTP به HTTPS برقرار باشد.
3. `/health` روی دامنهٔ نهایی HTTP 200 برگرداند.
4. ورود Super Admin و مشاهدهٔ داده‌ها و uploads فعلی موفق باشد.
5. یک تماس کنترل‌شده بدون خطای جدید در API و Realtime انجام شود.
6. backup و rollback قبل از اعلام انتشار نهایی کنترل شوند.

## دستورات مدیریت سرویس داخلی ویندوز

```powershell
cd C:\Users\arka\Documents\projects\ArkaCallCenterDemo
docker compose ps
docker compose logs --tail=100 api realtime web
docker compose restart

docker ps --filter name=arkacallcenter-artifacts
docker restart arkacallcenter-artifacts
```

برای توقف موقت سرویس دانلود از `docker stop arkacallcenter-artifacts` استفاده
شود. پوشهٔ دانلود حذف نمی‌شود و با `docker start arkacallcenter-artifacts`
دوباره در دسترس قرار می‌گیرد.
