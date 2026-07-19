# راه‌اندازی سمت ایزابل (Asterisk) — کامل و حرفه‌ای

این راهنما همه‌ی تنظیمات لازم روی سرور ایزابل (`192.168.10.101`) را پوشش می‌دهد تا
تماس‌ها به سامانه‌ی هوشمند آرکا (ورکر `ArkaCallCenter.Realtime`) وصل شوند.

معماری:

```
تماس‌گیرنده ──DID/اینباند──▶ [arka-main] (IVR پذیرش)
                               │ پخش پیام پذیرش + دریافت داخلی (DTMF)
                               ▼
                          [arka-ai] exten=داخلی
                               │ AudioSocket(UUID, WORKER:9092)
                               ▼
              ArkaCallCenter.Realtime ⇄ OpenAI gpt-realtime
```

## بازهٔ داخلی‌های انسانی (SIP)

بازهٔ `100` تا `300` برای تلفن‌های انسانی و softphone همکاران رزرو است. تخصیص داخلی‌های AI نباید از این بازه استفاده کند.

- داخلی‌های انسانی را از پنل Issabel بسازید تا در `users`، `devices`، `sip` و AstDB ثبت شوند.
- داخلی موجود `200` متعلق به `karami` است و نباید بازنویسی شود.
- در IVR ورودی، داخلی واقعی Issabel همیشه قبل از مسیر AI بررسی و به `ext-local` هدایت می‌شود.
- شمارهٔ رزروشده‌ای که هنوز در Issabel تعریف نشده است، نباید به AI fall through کند.
- برای softphoneهای داخل LAN از SIP/UDP روی پورت `5060` استفاده می‌شود. برای کاربران بیرون شبکه، VPN توصیه می‌شود و پورت SIP نباید بدون محدودیت روی اینترنت باز شود.

پارامترهای عمومی softphone:

```text
SIP server / domain: 192.168.10.101
Port:                5060
Transport:           UDP
Username/Auth ID:    شماره داخلی
Password:            secret همان داخلی در Issabel
Outbound proxy:      خالی
```

پس از ساخت یا ویرایش داخلی در Issabel، وضعیت رجیستر را بررسی کنید:

```bash
asterisk -rx "sip show peers"
asterisk -rx "sip show peer 222"
```

## ۰) پیش‌نیازها

```bash
# ماژول AudioSocket باید موجود باشد (Asterisk >= 16)
asterisk -rx "module show like audiosocket"
# اگر بارگذاری نشده:
asterisk -rx "module load app_audiosocket.so"
# برای دائمی‌شدن، در /etc/asterisk/modules.conf مطمئن شوید autoload=yes است
```

اگر ماژول وجود ندارد، بسته‌ی `asterisk-audiosocket` را نصب کنید (بسته به توزیع)
یا Asterisk را با پشتیبانی AudioSocket کامپایل کنید.

## ۱) نصب dialplan

```bash
# فایل‌ها را از این ریپو روی سرور کپی کنید:
scp telephony/extensions_arka.conf root@192.168.10.101:/etc/asterisk/
scp telephony/pjsip_custom.conf   root@192.168.10.101:/etc/asterisk/

# در /etc/asterisk/extensions_custom.conf این خط را اضافه کنید تا include شود:
#   #include extensions_arka.conf
```

سپس در `extensions_arka.conf` مقدار `ARKA_WORKER_HOST` را برابر IP سرور ورکر
(مثلاً `192.168.10.175`) بگذارید و reload کنید:

```bash
asterisk -rx "dialplan reload"
```

## ۲) هدایت شماره‌ی اصلی به IVR پذیرش

شماره‌ی اصلی شرکت (DID/اینباند) باید به context `[arka-main]` اکستنشن `s` برسد.

- **FreePBX:** یک *Custom Destination* بسازید با مقصد `arka-main,s,1`، سپس در
  *Inbound Routes* آن DID را به این Custom Destination هدایت کنید.
- **Asterisk خام:** در context اینباندتان: `exten => _X.,1,Goto(arka-main,s,1)`

## ۳) پیام پذیرش و موسیقی انتظار (از پنل سوپرادمین)

- **پیام پذیرش:** در پنل سوپرادمین → «پذیرش و انتظار» متن را وارد و ذخیره کنید.
  سامانه صوت WAV ۸kHz تولید و از طریق SCP در
  `/var/lib/asterisk/sounds/arka/main-greeting.wav` آپلود می‌کند (dialplan آن را
  با نام `arka/main-greeting` پخش می‌کند). برای این آپلود، اطلاعات SSH ایزابل باید
  در تنظیمات سرویس API موجود باشد (متغیرهای `Asterisk__Host/SshUser/SshPassword`).
- **موسیقی انتظار:** فایل WAV را در همان صفحه بارگذاری کنید؛ ورکر آن را حین
  «فکر کردن» هوش مصنوعی پخش می‌کند (نیازی به فایل روی ایزابل ندارد).

## ۴) اطلاعات SSH برای provisioning و آپلود صوت

سرویس API برای ساخت خودکار داخلی (PJSIP) و آپلود پیام پذیرش به SSH نیاز دارد.
این‌ها را به‌عنوان env به کانتینر `api` بدهید (در `docker-compose.yml` یا `.env`):

```
Asterisk__Host=192.168.10.101
Asterisk__SshUser=root
Asterisk__SshPassword=********
```

> بدون این‌ها، ساخت داخلی و آپلود صوت «شبیه‌سازی» می‌شود (اپ کار می‌کند ولی روی
> ایزابل چیزی ننویسد).

## ۵) شبکه و پورت‌ها

- ورکر آرکا روی TCP `9092` گوش می‌دهد؛ مطمئن شوید فایروال بین ایزابل و سرور ورکر
  این پورت را باز می‌گذارد.
- کدک صوتی AudioSocket: SLIN ۸kHz (ورکر داخلاً به ۲۴kHz برای OpenAI ری‌سمپل می‌کند).
- ضبط مکالمه در worker بر مبنای clock ثابت ۲۰ms ساخته می‌شود؛ صدای caller با فریم خروجی‌ای که واقعاً برای تماس پخش شده mix می‌شود. برای فعال‌شدن اصلاح ضبط، سرویس `realtime` باید rebuild و recreate شود.

## ۶) تست

```bash
# لاگ زنده‌ی استریسک
asterisk -rvvv
# سپس با شماره‌ی اصلی تماس بگیرید؛ باید پیام پذیرش پخش شود، داخلی را وارد کنید،
# و پاسخ هوش مصنوعی را بشنوید. لاگ ورکر:
docker compose logs -f realtime
```

## نکات عیب‌یابی
- «AudioSocket application not found» → ماژول `app_audiosocket` را بارگذاری کنید.
- پیام پذیرش پخش نمی‌شود → فایل `arka/main-greeting.wav` را در
  `/var/lib/asterisk/sounds/arka/` و مالکیت `asterisk:asterisk` را بررسی کنید.
- تماس وصل ولی صدایی نیست → پورت ۹۰۹۲، دسترسی شبکه، و کلید OpenAI را بررسی کنید.
