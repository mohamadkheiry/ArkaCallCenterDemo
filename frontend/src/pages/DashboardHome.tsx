import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Activity,
  ArrowLeft,
  BadgeCheck,
  BookOpenText,
  CheckCircle2,
  ChevronLeft,
  Clock3,
  Headphones,
  History,
  Mic2,
  Phone,
  PhoneCall,
  PlayCircle,
  Radio,
  Settings2,
  ShieldCheck,
  Sparkles,
  Timer,
  UserRound,
  WandSparkles,
  Zap,
} from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import { api } from '../lib/api'
import { Card } from '../components/ui'
import { toFa } from '../lib/format'

type IconType = React.ComponentType<{ size?: number | string; className?: string }>

const statTone = {
  indigo: {
    shell: 'from-indigo-50 via-white to-white border-indigo-100/80',
    icon: 'bg-indigo-600 text-white shadow-[0_10px_24px_rgba(79,70,229,.24)]',
    accent: 'text-indigo-600',
  },
  sky: {
    shell: 'from-sky-50 via-white to-white border-sky-100/80',
    icon: 'bg-sky-500 text-white shadow-[0_10px_24px_rgba(14,165,233,.22)]',
    accent: 'text-sky-600',
  },
  violet: {
    shell: 'from-violet-50 via-white to-white border-violet-100/80',
    icon: 'bg-violet-600 text-white shadow-[0_10px_24px_rgba(124,58,237,.22)]',
    accent: 'text-violet-600',
  },
} as const

function StatCard({
  title,
  value,
  sub,
  icon: Icon,
  tone,
  badge,
}: {
  title: string
  value: string
  sub: string
  icon: IconType
  tone: keyof typeof statTone
  badge?: string
}) {
  const colors = statTone[tone]
  return (
    <div
      className={`group relative overflow-hidden rounded-3xl border bg-gradient-to-bl p-5 shadow-[0_8px_30px_rgba(15,23,42,.055)] transition duration-300 hover:-translate-y-1 hover:shadow-[0_18px_40px_rgba(15,23,42,.09)] ${colors.shell}`}
    >
      <div className="absolute -left-8 -top-10 h-28 w-28 rounded-full bg-white/70 blur-2xl" />
      <div className="relative flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-xs font-semibold text-slate-500">
            <span>{title}</span>
            {badge && (
              <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-600">
                {badge}
              </span>
            )}
          </div>
          <div className="mt-3 truncate text-3xl font-extrabold tracking-tight text-slate-900">{value}</div>
          <div className="mt-1.5 flex items-center gap-1.5 text-[11px] text-slate-400">
            <span className={`h-1.5 w-1.5 rounded-full ${colors.accent} bg-current`} />
            {sub}
          </div>
        </div>
        <span className={`grid h-12 w-12 shrink-0 place-items-center rounded-2xl ${colors.icon}`}>
          <Icon size={21} />
        </span>
      </div>
    </div>
  )
}

function QuickAction({
  to,
  title,
  description,
  icon: Icon,
  tone,
}: {
  to: string
  title: string
  description: string
  icon: IconType
  tone: 'indigo' | 'amber' | 'sky' | 'rose'
}) {
  const tones = {
    indigo: 'bg-indigo-50 text-indigo-600 group-hover:bg-indigo-600',
    amber: 'bg-amber-50 text-amber-600 group-hover:bg-amber-500',
    sky: 'bg-sky-50 text-sky-600 group-hover:bg-sky-500',
    rose: 'bg-rose-50 text-rose-600 group-hover:bg-rose-500',
  }
  return (
    <Link
      to={to}
      className="group flex items-center gap-3 rounded-2xl border border-transparent p-3 transition-all hover:border-slate-200 hover:bg-white hover:shadow-soft"
    >
      <span
        className={`grid h-11 w-11 shrink-0 place-items-center rounded-2xl transition-all duration-300 group-hover:text-white group-hover:shadow-md ${tones[tone]}`}
      >
        <Icon size={20} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-bold text-slate-800">{title}</span>
        <span className="mt-0.5 block truncate text-[11px] text-slate-400">{description}</span>
      </span>
      <ChevronLeft size={17} className="text-slate-300 transition-transform group-hover:-translate-x-1 group-hover:text-slate-500" />
    </Link>
  )
}

function UsageRing({ value, unlimited }: { value: number; unlimited: boolean }) {
  const radius = 49
  const circumference = Math.PI * 2 * radius
  const offset = circumference - (Math.min(value, 100) / 100) * circumference
  return (
    <div className="relative h-36 w-36 shrink-0">
      <svg viewBox="0 0 120 120" className="-rotate-90">
        <circle cx="60" cy="60" r={radius} fill="none" stroke="#eef2f7" strokeWidth="10" />
        <circle
          cx="60"
          cy="60"
          r={radius}
          fill="none"
          stroke="url(#usageGradient)"
          strokeWidth="10"
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={unlimited ? circumference * 0.18 : offset}
          className="transition-all duration-1000"
        />
        <defs>
          <linearGradient id="usageGradient" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" stopColor="#6366f1" />
            <stop offset="100%" stopColor="#8b5cf6" />
          </linearGradient>
        </defs>
      </svg>
      <div className="absolute inset-0 grid place-items-center text-center">
        <div>
          <div className="text-2xl font-extrabold text-slate-900">{unlimited ? '∞' : `٪${toFa(value)}`}</div>
          <div className="mt-0.5 text-[10px] font-medium text-slate-400">{unlimited ? 'نامحدود' : 'مصرف شده'}</div>
        </div>
      </div>
    </div>
  )
}

const waveHeights = [14, 25, 18, 35, 48, 28, 55, 42, 22, 46, 31, 52, 36, 18, 27, 43, 24, 14]

export default function DashboardHome() {
  const { me } = useAuth()
  const [videoAvailable, setVideoAvailable] = useState(false)
  const sp = me?.smartPhone
  const isActive = sp?.status === 'Active' && sp.extension != null
  const limit = me?.callMinuteLimit ?? null
  const usedMinutes = me?.usedMinutes ?? 0
  const isUnlimited = me?.role === 'SuperAdmin'
  const usagePercent = useMemo(
    () => (limit && limit > 0 ? Math.min(100, Math.round((usedMinutes / limit) * 100)) : 0),
    [limit, usedMinutes],
  )

  useEffect(() => {
    api.get('/api/tutorial-video/info').then(({ data }) => setVideoAvailable(!!data.available)).catch(() => {})
  }, [])

  const greetingName = me?.firstName || me?.brandName || 'همراه عزیز'
  const receptionNumber = me?.receptionNumber ?? '02191008288'

  return (
    <div className="mx-auto max-w-7xl space-y-6 pb-8">
      <section className="dashboard-hero animate-in relative isolate overflow-hidden rounded-[2rem] px-6 py-7 text-white shadow-[0_26px_70px_rgba(30,27,75,.26)] sm:px-8 sm:py-9 lg:min-h-[340px] lg:px-10">
        <div className="dashboard-grid-pattern absolute inset-0 opacity-40" />
        <div className="dashboard-orb absolute -left-16 -top-16 h-56 w-56 rounded-full bg-violet-400/20 blur-3xl" />
        <div className="dashboard-orb absolute -bottom-24 right-1/3 h-64 w-64 rounded-full bg-cyan-300/10 blur-3xl" />

        <div className="relative grid h-full items-center gap-8 lg:grid-cols-[1.08fr_.92fr]">
          <div>
            <div className="mb-5 flex flex-wrap items-center gap-2.5">
              <span className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1.5 text-[11px] font-semibold text-indigo-50 backdrop-blur-md">
                <Sparkles size={14} />
                مرکز تماس هوشمند آرکا
              </span>
              <span
                className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-[11px] font-bold backdrop-blur-md ${
                  isActive ? 'bg-emerald-400/15 text-emerald-200' : 'bg-amber-400/15 text-amber-200'
                }`}
              >
                <span className={`relative flex h-2 w-2 rounded-full ${isActive ? 'bg-emerald-300' : 'bg-amber-300'}`}>
                  {isActive && <span className="absolute inset-0 animate-ping rounded-full bg-emerald-300 opacity-60" />}
                </span>
                {isActive ? 'آماده‌ی پاسخ‌گویی' : 'نیازمند راه‌اندازی'}
              </span>
            </div>

            <p className="text-sm font-medium text-indigo-200">سلام {greetingName}، خوش آمدید</p>
            <h1 className="mt-2 max-w-xl text-3xl font-extrabold leading-[1.45] tracking-tight sm:text-4xl">
              صدای کسب‌وکارتان،
              <span className="block bg-gradient-to-l from-cyan-200 via-white to-violet-200 bg-clip-text text-transparent">
                هوشمند و همیشه در دسترس
              </span>
            </h1>
            <p className="mt-4 max-w-xl text-sm leading-7 text-indigo-100/75 sm:text-[15px]">
              وضعیت تلفن، صدای گوینده و دانش اختصاصی کسب‌وکارتان را از یک فضای یکپارچه مدیریت کنید.
            </p>

            <div className="mt-7 flex flex-wrap gap-3">
              <Link
                to={isActive ? '/calls' : '/setup'}
                className="inline-flex h-12 items-center gap-2 rounded-2xl bg-white px-5 text-sm font-bold text-indigo-700 shadow-[0_10px_28px_rgba(255,255,255,.16)] transition hover:-translate-y-0.5 hover:bg-indigo-50"
              >
                {isActive ? <History size={18} /> : <WandSparkles size={18} />}
                {isActive ? 'مشاهده تماس‌ها' : 'راه‌اندازی تلفن'}
                <ArrowLeft size={16} />
              </Link>
              <Link
                to="/knowledge-base"
                className="inline-flex h-12 items-center gap-2 rounded-2xl border border-white/15 bg-white/10 px-5 text-sm font-semibold text-white backdrop-blur transition hover:-translate-y-0.5 hover:bg-white/15"
              >
                <BookOpenText size={17} />
                مدیریت پایگاه دانش
              </Link>
            </div>
          </div>

          <div className="relative mx-auto w-full max-w-md lg:mr-auto">
            <div className="absolute -inset-5 rounded-[2.5rem] bg-gradient-to-bl from-cyan-400/15 to-violet-400/20 blur-2xl" />
            <div className="relative overflow-hidden rounded-[1.75rem] border border-white/15 bg-slate-950/35 p-5 shadow-2xl backdrop-blur-xl sm:p-6">
              <div className="flex items-center justify-between border-b border-white/10 pb-4">
                <div className="flex items-center gap-3">
                  <span className="dashboard-glow-pulse grid h-11 w-11 place-items-center rounded-2xl bg-gradient-to-br from-indigo-400 to-violet-500 text-white">
                    <Headphones size={21} />
                  </span>
                  <div>
                    <div className="text-sm font-bold">دستیار صوتی آرکا</div>
                    <div className="mt-0.5 flex items-center gap-1.5 text-[10px] text-emerald-300">
                      <Radio size={11} />
                      سرویس آنلاین است
                    </div>
                  </div>
                </div>
                <span className="rounded-xl bg-white/8 px-3 py-1.5 text-[10px] text-indigo-100">
                  داخلی {sp?.extension != null ? toFa(sp.extension) : '—'}
                </span>
              </div>

              <div className="py-6">
                <div className="mb-3 flex items-center justify-between text-[10px] text-indigo-200/70">
                  <span>پردازش زنده صدا</span>
                  <span>AI VOICE</span>
                </div>
                <div dir="ltr" className="flex h-16 items-center justify-center gap-1.5 rounded-2xl bg-white/[.055] px-4">
                  {waveHeights.map((height, index) => (
                    <span
                      key={index}
                      className="dashboard-wave-bar w-1 rounded-full bg-gradient-to-t from-indigo-400 to-cyan-200"
                      style={{ height, animationDelay: `${index * 70}ms` }}
                    />
                  ))}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="rounded-2xl bg-white/[.06] p-3.5">
                  <div className="flex items-center gap-1.5 text-[10px] text-indigo-200/70">
                    <PhoneCall size={12} />
                    شماره پذیرش
                  </div>
                  <div dir="ltr" className="mt-2 text-right text-sm font-bold tracking-wide text-white">
                    {toFa(receptionNumber)}
                  </div>
                </div>
                <div className="rounded-2xl bg-white/[.06] p-3.5">
                  <div className="flex items-center gap-1.5 text-[10px] text-indigo-200/70">
                    <Mic2 size={12} />
                    صدای منتخب
                  </div>
                  <div className="mt-2 truncate text-sm font-bold text-white">{me?.voiceName ?? 'پیش‌فرض'}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="stagger grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard
          title="شماره داخلی اختصاصی"
          value={sp?.extension != null ? toFa(sp.extension) : '—'}
          sub={isActive ? 'متصل به مرکز تماس' : sp ? 'در حال آماده‌سازی' : 'هنوز ساخته نشده'}
          badge={isActive ? 'فعال' : undefined}
          icon={Phone}
          tone="indigo"
        />
        <StatCard
          title="دقایق استفاده‌شده"
          value={toFa(usedMinutes)}
          sub={isUnlimited ? 'دسترسی نامحدود مدیریتی' : limit ? `از ${toFa(limit)} دقیقه` : 'بر اساس پلن فعال'}
          icon={Timer}
          tone="sky"
        />
        <StatCard
          title="صدای گوینده"
          value={me?.voiceName ?? 'پیش‌فرض'}
          sub="قابل شخصی‌سازی و تغییر"
          icon={Mic2}
          tone="violet"
        />
      </div>

      {!isActive && (
        <div className="animate-in overflow-hidden rounded-3xl border border-amber-200/70 bg-gradient-to-l from-amber-50 via-white to-white p-5 shadow-soft sm:p-6">
          <div className="flex flex-col items-start justify-between gap-5 sm:flex-row sm:items-center">
            <div className="flex items-start gap-4">
              <span className="grid h-12 w-12 shrink-0 place-items-center rounded-2xl bg-amber-100 text-amber-600">
                <Zap size={22} />
              </span>
              <div>
                <h2 className="font-extrabold text-slate-900">تلفن هوشمندتان را در چند دقیقه فعال کنید</h2>
                <p className="mt-1.5 max-w-2xl text-sm leading-6 text-slate-500">
                  دانش کسب‌وکار، پیام خوش‌آمد و صدای گوینده را مشخص کنید؛ بقیه‌ی مراحل را آرکا برایتان انجام می‌دهد.
                </p>
              </div>
            </div>
            <Link
              to="/setup"
              className="inline-flex h-11 shrink-0 items-center gap-2 rounded-xl bg-amber-500 px-4 text-sm font-bold text-white shadow-[0_8px_20px_rgba(245,158,11,.24)] transition hover:-translate-y-0.5 hover:bg-amber-600"
            >
              شروع راه‌اندازی
              <ArrowLeft size={16} />
            </Link>
          </div>
        </div>
      )}

      <div className="grid gap-6 xl:grid-cols-[.9fr_1.1fr]">
        <Card className="animate-in !rounded-3xl !border-slate-200/70 !p-0">
          <div className="flex items-center justify-between border-b border-slate-100 px-5 py-4 sm:px-6">
            <div>
              <h2 className="font-extrabold text-slate-900">مصرف سرویس</h2>
              <p className="mt-1 text-[11px] text-slate-400">نمای کلی از سهمیه مکالمه شما</p>
            </div>
            <span className="grid h-10 w-10 place-items-center rounded-2xl bg-indigo-50 text-indigo-600">
              <Activity size={19} />
            </span>
          </div>
          <div className="flex flex-col items-center gap-6 p-5 sm:flex-row sm:p-6">
            <UsageRing value={usagePercent} unlimited={isUnlimited} />
            <div className="w-full flex-1 space-y-3">
              <div className="flex items-center justify-between rounded-2xl bg-slate-50 px-4 py-3">
                <span className="flex items-center gap-2 text-xs text-slate-500">
                  <Clock3 size={15} className="text-indigo-500" />
                  مصرف‌شده
                </span>
                <span className="text-sm font-extrabold text-slate-900">{toFa(usedMinutes)} دقیقه</span>
              </div>
              <div className="flex items-center justify-between rounded-2xl bg-slate-50 px-4 py-3">
                <span className="flex items-center gap-2 text-xs text-slate-500">
                  <ShieldCheck size={15} className="text-emerald-500" />
                  سقف دسترسی
                </span>
                <span className="text-sm font-extrabold text-slate-900">
                  {isUnlimited ? 'نامحدود' : limit ? `${toFa(limit)} دقیقه` : 'پلن پایه'}
                </span>
              </div>
              {!isUnlimited && limit && (
                <div className="pt-1">
                  <div className="mb-2 flex justify-between text-[10px] text-slate-400">
                    <span>وضعیت سهمیه</span>
                    <span>٪{toFa(usagePercent)}</span>
                  </div>
                  <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                    <div
                      className="h-full rounded-full bg-gradient-to-l from-indigo-500 to-violet-500 transition-all duration-1000"
                      style={{ width: `${usagePercent}%` }}
                    />
                  </div>
                </div>
              )}
            </div>
          </div>
        </Card>

        <Card className="animate-in !rounded-3xl !border-slate-200/70 !p-0">
          <div className="flex items-center justify-between border-b border-slate-100 px-5 py-4 sm:px-6">
            <div>
              <h2 className="font-extrabold text-slate-900">دسترسی سریع</h2>
              <p className="mt-1 text-[11px] text-slate-400">مدیریت بخش‌های پرکاربرد سامانه</p>
            </div>
            <span className="grid h-10 w-10 place-items-center rounded-2xl bg-violet-50 text-violet-600">
              <Sparkles size={19} />
            </span>
          </div>
          <div className="grid gap-1 p-3 sm:grid-cols-2 sm:p-4">
            <QuickAction
              to="/knowledge-base"
              title="پایگاه دانش"
              description="ویرایش اطلاعات پاسخ‌گویی"
              icon={BookOpenText}
              tone="indigo"
            />
            <QuickAction
              to="/voice"
              title="صدای گوینده"
              description="انتخاب لحن و صدای مناسب"
              icon={Mic2}
              tone="amber"
            />
            <QuickAction
              to="/smartphone"
              title="تلفن هوشمند"
              description="تنظیم پیام خوش‌آمدگویی"
              icon={Settings2}
              tone="sky"
            />
            <QuickAction
              to="/calls"
              title="گزارش تماس‌ها"
              description="تاریخچه و مکالمات اخیر"
              icon={History}
              tone="rose"
            />
          </div>
        </Card>
      </div>

      {isActive && (
        <section className="animate-in overflow-hidden rounded-3xl border border-emerald-200/60 bg-white shadow-soft">
          <div className="grid lg:grid-cols-[1fr_auto]">
            <div className="p-5 sm:p-7">
              <div className="mb-5 flex items-center gap-3">
                <span className="grid h-11 w-11 place-items-center rounded-2xl bg-emerald-50 text-emerald-600">
                  <PhoneCall size={21} />
                </span>
                <div>
                  <h2 className="font-extrabold text-slate-900">آماده‌ی یک تماس آزمایشی هستید؟</h2>
                  <p className="mt-1 text-xs text-slate-400">در دو مرحله کیفیت پاسخ‌گویی دستیار را بررسی کنید.</p>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="flex items-start gap-3 rounded-2xl bg-slate-50 p-4">
                  <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-emerald-500 text-xs font-bold text-white">۱</span>
                  <div>
                    <div className="text-xs font-bold text-slate-800">تماس با شماره پذیرش</div>
                    <div dir="ltr" className="mt-1 text-right text-sm font-extrabold text-emerald-600">{toFa(receptionNumber)}</div>
                  </div>
                </div>
                <div className="flex items-start gap-3 rounded-2xl bg-slate-50 p-4">
                  <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-indigo-500 text-xs font-bold text-white">۲</span>
                  <div>
                    <div className="text-xs font-bold text-slate-800">شماره‌گیری داخلی اختصاصی</div>
                    <div className="mt-1 text-sm font-extrabold text-indigo-600">{toFa(sp!.extension!)}#</div>
                  </div>
                </div>
              </div>
            </div>
            <div className="flex min-w-52 flex-col items-center justify-center bg-gradient-to-bl from-emerald-500 to-teal-600 p-6 text-center text-white">
              <span className="grid h-14 w-14 place-items-center rounded-full bg-white/15 ring-8 ring-white/5">
                <Phone size={25} />
              </span>
              <span className="mt-4 text-sm font-bold">خط شما فعال است</span>
              <span className="mt-1 flex items-center gap-1 text-[10px] text-emerald-100">
                <CheckCircle2 size={12} />
                آماده پاسخ‌گویی
              </span>
            </div>
          </div>
        </section>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        {videoAvailable && (
          <Card className="animate-in !rounded-3xl !p-5 sm:!p-6">
            <div className="mb-4 flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <span className="grid h-10 w-10 place-items-center rounded-2xl bg-indigo-50 text-indigo-600">
                  <PlayCircle size={20} />
                </span>
                <div>
                  <h3 className="text-sm font-extrabold text-slate-900">ویدیوی آموزشی</h3>
                  <p className="mt-0.5 text-[10px] text-slate-400">آشنایی سریع با امکانات آرکا</p>
                </div>
              </div>
            </div>
            <video src="/api/tutorial-video" controls className="aspect-video w-full rounded-2xl bg-slate-950 object-cover" preload="metadata" />
          </Card>
        )}

        <Card className={`animate-in !rounded-3xl !p-5 sm:!p-6 ${videoAvailable ? '' : 'lg:col-span-2'}`}>
          <div className="mb-5 flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <span className="grid h-10 w-10 place-items-center rounded-2xl bg-slate-100 text-slate-600">
                <UserRound size={20} />
              </span>
              <div>
                <h3 className="text-sm font-extrabold text-slate-900">حساب و سرویس شما</h3>
                <p className="mt-0.5 text-[10px] text-slate-400">اطلاعات مالکیت پنل</p>
              </div>
            </div>
            <BadgeCheck size={20} className="text-emerald-500" />
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-2xl bg-slate-50 p-4">
              <div className="text-[10px] text-slate-400">نام تجاری</div>
              <div className="mt-2 truncate text-sm font-bold text-slate-800">{me?.brandName || 'آرکا'}</div>
            </div>
            <div className="rounded-2xl bg-slate-50 p-4">
              <div className="text-[10px] text-slate-400">شماره همراه</div>
              <div dir="ltr" className="mt-2 text-right text-sm font-bold text-slate-800">{toFa(me?.phoneNumber ?? '')}</div>
            </div>
            <div className="rounded-2xl bg-slate-50 p-4">
              <div className="text-[10px] text-slate-400">سطح دسترسی</div>
              <div className="mt-2 text-sm font-bold text-slate-800">{isUnlimited ? 'مدیریت کل' : 'کاربر سازمانی'}</div>
            </div>
          </div>
        </Card>
      </div>
    </div>
  )
}
