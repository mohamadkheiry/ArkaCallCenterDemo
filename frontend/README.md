# داشبورد کال‌سنتر آرکا

رابط فارسی راست‌چین با React 19، TypeScript، Vite، Tailwind و Vazirmatn محلی.
نسخهٔ فعلی همچنان از پایگاه دانش متن/فایل استفاده می‌کند.

## اجرا و بررسی

```sh
npm ci
npm run dev -- --host 127.0.0.1
npm run build
npm run lint
npm run preview -- --host 127.0.0.1
```

API توسعه در `http://localhost:5080` است. مسیرهای `/api` و `/health` در `vite.config.ts`
پروکسی می‌شوند. خروجی قابل انتشار در `dist` است؛ آن را با Vite preview یا Nginx بررسی
کنید تا مسیر فونت‌ها و bundleها نیز دقیقاً شبیه خروجی production باشند.

## نقشه رابط

| فایل یا پوشه | مسئولیت |
| --- | --- |
| `components/DashboardLayout.tsx` | هدر، منو، پروفایل، فوکوس/اسکرول موبایل و راهنما |
| `components/ui.tsx` | دکمه، ورودی، سطح، لوگو، بارگذاری و اسلایدر مشترک |
| `workspace.css` | پالت، تایپوگرافی، پوسته، فرم، جدول و breakpointهای داشبورد |
| `index.css` | Tailwind، فونت‌های محلی و انیمیشن‌های مشترک |
| `login.css` و `pages/LoginPage.tsx` | طراحی مستقل ورود، پنل برند و فرم SMS/Call |
| `pages/login/` | hook جریان ورود و ورودی قابل‌دسترس کد تأیید |
| `pages/DashboardHome.tsx` | وضعیت واقعی تلفن، مصرف سهمیه و دسترسی‌های سریع |
| `pages/KnowledgeBasePage.tsx` | دانش متنی/فایلی و پیام نتیجه |
| `pages/CallsPage.tsx` | جست‌وجو، فیلتر، ضبط تماس و سوالات بی‌پاسخ |
| `pages/VoicePage.tsx` | انتخاب و شنیدن گوینده، بدون دکمه تو‌در‌تو |
| `pages/admin/AdminPage.tsx` | منوی مدیریتی با انتخاب تب در query string |
| `context/AuthContext.tsx` | ورود، پروفایل و مشاهده پنل کاربر توسط سوپرادمین |

برای کنترل‌های جدید از خانواده مشترک استفاده کنید؛ رنگ و فاصلهٔ یک‌بارمصرف نسازید.
شماره‌ها را با `toFa` و جهت درست نمایش دهید و در جست‌وجو `toEn` را رعایت کنید.
تمام داده‌ها از API می‌آیند؛ اطلاعات نمونه QA نباید به bundle اجرایی اضافه شود.

جزئیات طراحی، تفاوت‌های عمدی با مرجع و ماتریس تست:
[DASHBOARD_DESIGN.md](../docs/DASHBOARD_DESIGN.md).
طراحی، assetها و قراردادهای صفحه ورود:
[LOGIN_DESIGN.md](../docs/LOGIN_DESIGN.md).
برای انتشار فقط وب و rollback:
[deployment.md](../deployment.md).
