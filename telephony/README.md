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
- الگوی context `arka-ai` باید `_X!` باشد. wildcard نقطه در `_X.` حداقل یک رقم اضافه می‌خواهد و داخلی تک‌رقمی مانند `2` را رد می‌کند؛ `!` پس از اعتبارسنجی `arka-main` داخلی تک‌رقمی و چندرقمی را پوشش می‌دهد.
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

## Cisco 7942G و داخلی ۲۲۲

در محیط عملیاتی فعلی، گوشی Cisco 7942G با MAC رزروشده و IP ثابت
`192.168.10.170` به داخلی `222` اختصاص دارد. فایل تنظیم آن در TFTP با نام
`SEP44E4D944E996.cnf.xml` نگهداری می‌شود و load پایدار آن
`SIP42.8-5-4S` است. فایل‌های firmware امضاشدهٔ Cisco باید فقط روی TFTP
مرکز تلفن باشند و به علت مجوز انتشار و حجم باینری در Git قرار نمی‌گیرند.

این firmware قدیمی REGISTER را از یک پورت موقت ارسال می‌کند، پاسخ Digest را
در این محیط کامل نمی‌کند و تماس ورودی را روی UDP/5060 می‌پذیرد. بنابراین peer
داخلی ۲۲۲ در `/etc/asterisk/sip_custom_post.conf` به‌صورت dynamic و بدون Digest،
اما فقط برای IP ثابت گوشی، تعریف می‌شود. نمونهٔ قابل‌استقرار در
`telephony/cisco/sip_custom_post.conf.example` قرار دارد:

```ini
[222](+)
secret=
deny=0.0.0.0/0.0.0.0
permit=192.168.10.170/255.255.255.255
qualify=no
nat=no
host=dynamic
port=5060
insecure=port,invite
```

این حالت فقط برای LAN قابل‌اعتماد مجاز است. IP گوشی باید در DHCP رزرو شود و
ترجیحاً MAC آن روی پورت سوئیچ محدود باشد؛ این الگو نباید برای SIP روی اینترنت
استفاده شود. رمزها و اطلاعات SSH گوشی یا PBX نباید در Git ثبت شوند.

`insecure=port,invite` فقط ناسازگاری firmware قدیمی Cisco با پورت مبدأ موقت را
جبران می‌کند؛ ACL بالا باید حتماً همراه آن باشد تا INVITE بدون Digest فقط از
IP ثابت همین گوشی پذیرفته شود.

شبکهٔ تلفن‌های مدیریت‌شده باید در jail مربوط به Asterisk در Fail2ban نادیده
گرفته شود تا تلاش‌های رجیستر firmware قدیمی، IP تلفن را مسدود نکند. مقدارهای
قبلی `ignoreip` را حفظ کنید و شبکهٔ LAN را به همان خط اضافه کنید:

```ini
[asterisk]
ignoreip = <existing-values> 192.168.10.0/24
```

اعمال فوری و خارج‌کردن گوشی از مسدودی:

```bash
fail2ban-client set asterisk addignoreip 192.168.10.0/24
fail2ban-client set asterisk unbanip 192.168.10.170
fail2ban-client status asterisk
```

فایل Dial Plan گوشی باید به `/tftpboot/dialplan.xml` کپی شود. نمونهٔ کنترل‌شده
در `telephony/cisco/dialplan.xml` قرار دارد؛ موبایل‌های ۱۱رقمی `09...`،
شماره‌های ثابت هشت‌رقمی تهران و شماره‌های ۱۱رقمی دارای پیش‌شمارهٔ `021`
بلافاصله ارسال می‌شوند. برای جلوگیری از قطع‌شدن شماره‌های عمومی بعد از سه رقم،
داخلی‌های سه‌رقمی با timeout سه‌ثانیه‌ای ارسال می‌شوند؛ سایر شماره‌ها پس از
۵ ثانیه ارسال خواهند شد. پس از تغییر، گوشی باید واقعاً reboot
شود تا فایل را دوباره از TFTP دریافت کند؛ قطع و وصل صرفِ کابل شبکه در صورت وجود
آداپتور برق کافی نیست.

گوشی برای بالا آمدن به NTP نیاز دارد. `chronyd` روی PBX باید UDP/123 را فقط
برای شبکهٔ داخلی ارائه کند؛ نمونهٔ محدودیت در `/etc/chrony.conf`:

```conf
allow 192.168.10.0/24
```

تست عملیاتی مقصد ۲۲۲:

```bash
asterisk -rx "sip show peer 222"
# Addr->IP باید 192.168.10.170:5060 باشد.
# Dynamic باید Yes، Insecure باید port,invite و ACL باید Yes باشد.
# در trace تماس باید پاسخ‌های 100 Trying و 180 Ringing دیده شوند.
```

همین الگوی dynamic برای داخلی فیزیکی `200` نیز لازم است. تعریف static مانند
`host=192.168.10.186` باعث می‌شود Asterisk درخواست REGISTER گوشی را با پیام
`Peer is not supposed to register` رد کند و تماس ورودی با `CHANUNAVAIL` پایان
یابد. برای این گوشی، بخش `[200](+)` در فایل نمونه استفاده و ACL فقط به
`192.168.10.186/32` محدود می‌شود. پس از `sip reload` باید خروجی
`sip show peer 200` شامل `Dynamic: Yes`، Contact معتبر و User-Agent گوشی باشد.
تست کنترل‌شده از `ext-local,200` باید وضعیت `Ringing` بگیرد و پس از پایان تست
هیچ channel فعالی باقی نماند.

برای تماس خروجی، شماره‌های موبایل ایران باید مستقیم با قالب `09xxxxxxxxx`
شماره‌گیری شوند. route خروجی باید الگوی `_0.` را به trunk مربوط بفرستد؛
در تست کنترل‌شده، ورود به `SIP/shatel/09xxxxxxxxx` و دریافت Progress/Ringing باید
پیش از پایان تماس دیده شود. شماره‌های واقعی در مستندات یا Git ثبت نشوند.

برای شماره‌های ثابت داخل تهران، کاربر فقط هشت رقم را می‌گیرد. در Outbound Route
فعال Issabel یک Dial Pattern با `match pattern = XXXXXXXX` و
`prepend = 021` اضافه کنید. خروجی تولیدشده باید مشابه زیر باشد:

```asterisk
exten => _XXXXXXXX,n,Macro(dialout-trunk,1,021${EXTEN},,off)
```

پس از Apply Config، وجود الگو را با دستور زیر کنترل کنید:

```bash
asterisk -rx "dialplan show 55005013@from-internal"
# Macro dialout-trunk باید 021${EXTEN} را نشان دهد.
```

پذیرش نهایی باید هر دو قالب هشت‌رقمی و `021`+هشت‌رقم را پوشش دهد. در لاگ SIP
بررسی کنید که گوشی کل شماره را در INVITE فرستاده باشد؛ مشاهدهٔ INVITE سه‌رقمی
هنگام شماره‌گیری مقصد عمومی نشان می‌دهد گوشی هنوز `dialplan.xml` قبلی را در
حافظه دارد و باید reboot شود. سپس هر دو قالب باید در نهایت به شمارهٔ کامل
`021xxxxxxxx` روی trunk برسند و پاسخ Progress یا Ringing دریافت کنند.

مسیر تماس ورودی شمارهٔ اصلی ابتدا `arka-main` را اجرا می‌کند، وجود
`DEVICE/222/dial` را در AstDB می‌سنجد و سپس به `ext-local,222,1` می‌رود؛
بنابراین این تنظیم با داخلی‌های AI و داشبورد تداخل ندارد.
