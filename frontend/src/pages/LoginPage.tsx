import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ArrowLeft,
  AudioLines,
  Bot,
  CircleAlert,
  CircleCheck,
  Database,
  Pencil,
  Phone,
  PhoneCall,
  ShieldCheck,
  Smartphone,
  Sparkles,
} from 'lucide-react'
import { api, apiError } from '../lib/api'
import { toEn, toFa } from '../lib/format'
import { useAuth } from '../context/AuthContext'
import { Button, Logo, cn } from '../components/ui'

const OTP_LEN = 6

/** ارتفاع میله‌های موج صوتی (تصویرِ ایستا هم زیباست؛ انیمیشن فقط scale می‌کند) */
const WAVE = [10, 18, 8, 24, 14, 32, 20, 38, 26, 40, 22, 34, 14, 26, 10, 18, 8]

const FEATURES = [
  {
    icon: AudioLines,
    title: 'پاسخ‌گویی صوتی با هوش مصنوعی',
    desc: 'گفت‌وگوی طبیعی و روان با مشتریان، با صدای انتخابی خودتان',
  },
  {
    icon: Database,
    title: 'پایگاه دانش اختصاصی (RAG)',
    desc: 'پاسخ‌ها دقیقاً بر اساس اسناد و دانش کسب‌وکار شما',
  },
  {
    icon: PhoneCall,
    title: 'داخلی اختصاصی روی سامانه',
    desc: 'شماره داخلی مستقل، آماده‌ی پاسخ‌گویی بی‌وقفه',
  },
]

export default function LoginPage() {
  const [step, setStep] = useState<'phone' | 'otp'>('phone')
  const [phone, setPhone] = useState('')
  const [code, setCode] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [callLoading, setCallLoading] = useState(false)
  const [callMsg, setCallMsg] = useState('')
  const [otpFocused, setOtpFocused] = useState(false)
  const { setToken } = useAuth()
  const navigate = useNavigate()

  async function requestOtpByCall() {
    setError('')
    setCallMsg('')
    setCallLoading(true)
    try {
      await api.post('/api/auth/request-otp-call', { phoneNumber: toEn(phone) })
      setCallMsg('در حال تماس با شما… کد به‌صورت صوتی و رقم‌به‌رقم خوانده می‌شود.')
    } catch (err) {
      setError(apiError(err))
    } finally {
      setCallLoading(false)
    }
  }

  async function requestOtp(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await api.post('/api/auth/request-otp', { phoneNumber: toEn(phone) })
      setStep('otp')
    } catch (err) {
      setError(apiError(err))
    } finally {
      setLoading(false)
    }
  }

  async function verifyOtp(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const { data } = await api.post('/api/auth/verify-otp', {
        phoneNumber: toEn(phone),
        code: toEn(code),
      })
      setToken(data.token)
      navigate(data.profileCompleted ? '/' : '/onboarding', { replace: true })
    } catch (err) {
      setError(apiError(err))
    } finally {
      setLoading(false)
    }
  }

  function backToPhone() {
    setStep('phone')
    setCode('')
    setError('')
    setCallMsg('')
  }

  const codeDigits = toEn(code)

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[1.08fr_1fr]">
      {/* ─────────── پنل برند (سمت راست در RTL) ─────────── */}
      <aside className="relative hidden overflow-hidden lg:block">
        {/* لایه‌های پس‌زمینه: شفق + شبکه + هاله‌ها + گرین */}
        <div className="login-aurora absolute inset-0" />
        <div
          className="absolute inset-0 opacity-[0.09] [background-image:linear-gradient(rgba(255,255,255,0.5)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.5)_1px,transparent_1px)] [background-size:46px_46px] [mask-image:radial-gradient(ellipse_62%_52%_at_50%_40%,black,transparent)]"
          aria-hidden="true"
        />
        <div className="absolute -left-28 -top-28 h-96 w-96 rounded-full bg-brand-500/30 blur-3xl" aria-hidden="true" />
        <div className="absolute -bottom-24 -right-16 h-[26rem] w-[26rem] rounded-full bg-violet-500/20 blur-3xl" aria-hidden="true" />
        <div className="login-grain absolute inset-0 opacity-[0.05] mix-blend-overlay" aria-hidden="true" />

        <div className="relative flex h-full flex-col justify-between p-12 xl:p-16">
          {/* سربرگ: لوگو در قابِ شیشه‌ای روشن + نشان محصول */}
          <div className="flex items-center justify-between gap-4">
            <div className="rounded-2xl bg-white/95 px-4 py-2.5 shadow-soft-lg ring-1 ring-white/40 backdrop-blur">
              <Logo size={40} />
            </div>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-white/15 bg-white/[0.07] px-3.5 py-1.5 text-xs font-medium text-white/75 backdrop-blur-md">
              <Sparkles size={13} className="text-brand-200" />
              سامانه تلفن هوشمند ابری
            </span>
          </div>

          {/* بدنه: تیتر، موتیف صوتی، ویژگی‌ها */}
          <div className="space-y-10">
            <div className="space-y-4">
              <h1 className="text-4xl font-extrabold leading-[1.45] text-white xl:text-[2.75rem]">
                منشی هوشمند شما،
                <br />
                <span className="bg-gradient-to-l from-brand-200 via-white to-brand-300 bg-clip-text text-transparent">
                  همیشه پاسخگو
                </span>
              </h1>
              <p className="max-w-md text-base leading-8 text-white/65 xl:text-lg xl:leading-9">
                با هوش مصنوعی، تماس‌های کسب‌وکارتان را بر اساس پایگاه دانش اختصاصی خودتان پاسخ
                دهید — بی‌وقفه، طبیعی و حرفه‌ای.
              </p>
            </div>

            {/* موتیف «هوش مصنوعی پاسخ می‌دهد»: تماس ← موج صدا ← هسته‌ی AI */}
            <div className="relative flex items-center gap-5 py-3" dir="ltr" aria-hidden="true">
              {/* تماس‌گیرنده */}
              <div className="login-float grid h-16 w-16 shrink-0 place-items-center rounded-2xl border border-white/15 bg-white/[0.07] text-white/85 shadow-soft-lg backdrop-blur-md">
                <Phone size={24} />
              </div>

              {/* موج صوتی */}
              <div className="flex h-12 flex-1 items-center justify-center gap-[5px] overflow-hidden">
                {WAVE.map((h, i) => (
                  <span
                    key={i}
                    className="login-bar w-[3px] shrink-0 rounded-full bg-gradient-to-t from-brand-300/80 to-white/90"
                    style={{ height: h, animationDelay: `${(i % 7) * 0.14}s` }}
                  />
                ))}
              </div>

              {/* هسته‌ی هوش مصنوعی */}
              <div className="relative grid shrink-0 place-items-center">
                <span className="login-orbit absolute -inset-5 rounded-full border border-dashed border-white/20">
                  <span className="absolute -top-[3px] left-1/2 h-1.5 w-1.5 -translate-x-1/2 rounded-full bg-brand-200" />
                </span>
                <span className="login-pulse-ring absolute inset-0 rounded-full border border-white/30" />
                <span className="login-pulse-ring absolute inset-0 rounded-full border border-white/20 [animation-delay:1.3s]" />
                <div className="grid h-[4.5rem] w-[4.5rem] place-items-center rounded-full bg-gradient-to-br from-brand-400 to-brand-700 text-white shadow-brand ring-1 ring-white/30">
                  <Bot size={30} />
                </div>
              </div>
            </div>

            {/* ویژگی‌ها در کارت‌های شیشه‌ای */}
            <ul className="stagger max-w-md space-y-3">
              {FEATURES.map(({ icon: Icon, title, desc }) => (
                <li
                  key={title}
                  className="flex items-start gap-3.5 rounded-2xl border border-white/10 bg-white/[0.06] p-4 backdrop-blur-md transition-colors duration-300 hover:border-white/20 hover:bg-white/[0.1]"
                >
                  <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-gradient-to-br from-white/15 to-white/5 text-brand-200 ring-1 ring-white/15">
                    <Icon size={19} />
                  </span>
                  <div>
                    <div className="text-sm font-bold text-white">{title}</div>
                    <div className="mt-1 text-[13px] leading-6 text-white/55">{desc}</div>
                  </div>
                </li>
              ))}
            </ul>
          </div>

          {/* پانوشت */}
          <div className="flex items-center justify-between text-xs text-white/40">
            <span>© آرکا — سامانه تلفن هوشمند</span>
            <span className="inline-flex items-center gap-1.5">
              <ShieldCheck size={14} className="text-brand-200/70" />
              ورود امن با رمز یک‌بارمصرف
            </span>
          </div>
        </div>
      </aside>

      {/* ─────────── فرم ورود ─────────── */}
      <main className="relative flex min-h-screen items-center justify-center overflow-hidden px-5 py-10 sm:px-8 lg:min-h-full">
        {/* هاله‌های ملایم پس‌زمینه‌ی سمت فرم */}
        <div className="absolute -top-32 left-1/2 h-80 w-[36rem] -translate-x-1/2 rounded-full bg-brand-100/60 blur-3xl" aria-hidden="true" />
        <div className="absolute -bottom-40 -left-24 h-72 w-72 rounded-full bg-brand-50 blur-3xl" aria-hidden="true" />

        <div className="relative w-full max-w-[26.5rem]">
          {/* لوگو در موبایل (پنل برند مخفی است) */}
          <div className="mb-8 flex justify-center lg:hidden">
            <Logo size={48} />
          </div>

          <div className="animate-in relative overflow-hidden rounded-[1.75rem] border border-white/80 bg-white/80 p-7 shadow-soft-lg backdrop-blur-xl sm:p-9">
            {/* خطِ نورِ لبه‌ی بالای کارت */}
            <div
              className="absolute inset-x-8 top-0 h-px bg-gradient-to-l from-transparent via-brand-400/60 to-transparent"
              aria-hidden="true"
            />

            {/* نشانگر مرحله */}
            <div className="mb-7 flex items-center justify-between">
              <div className="flex items-center gap-1.5" aria-hidden="true">
                <span className="h-1.5 w-9 rounded-full bg-gradient-to-l from-brand-500 to-brand-400" />
                <span
                  className={cn(
                    'h-1.5 w-9 rounded-full transition-colors duration-500',
                    step === 'otp' ? 'bg-gradient-to-l from-brand-500 to-brand-400' : 'bg-slate-200',
                  )}
                />
              </div>
              <span className="text-xs font-medium text-slate-400">
                مرحله {step === 'phone' ? toFa(1) : toFa(2)} از {toFa(2)}
              </span>
            </div>

            {step === 'phone' ? (
              <div key="phone" className="login-step">
                <div className="mb-6">
                  <div className="mb-5 grid h-14 w-14 place-items-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-brand">
                    <Smartphone size={24} />
                  </div>
                  <h2 className="text-[1.6rem] font-extrabold tracking-tight text-slate-800">
                    ورود به داشبورد
                  </h2>
                  <p className="mt-2 text-sm leading-7 text-slate-500">
                    شماره موبایل خود را وارد کنید تا کد ورود برایتان ارسال شود.
                  </p>
                </div>

                <form onSubmit={requestOtp} className="space-y-5">
                  <div>
                    <label htmlFor="login-phone" className="mb-2 block text-sm font-semibold text-slate-700">
                      شماره موبایل
                    </label>
                    <div className="group relative" dir="ltr">
                      <span className="pointer-events-none absolute inset-y-0 left-4 flex items-center text-slate-300 transition-colors duration-200 group-focus-within:text-brand-500">
                        <Smartphone size={19} />
                      </span>
                      <input
                        id="login-phone"
                        dir="ltr"
                        inputMode="numeric"
                        autoComplete="tel"
                        autoFocus
                        required
                        placeholder="09xxxxxxxxx"
                        value={phone}
                        onChange={(e) => setPhone(e.target.value)}
                        className="h-14 w-full rounded-2xl border border-slate-200 bg-white pl-12 pr-4 text-center text-lg font-semibold tracking-[0.15em] text-slate-800 shadow-soft outline-none transition-all duration-200 placeholder:text-base placeholder:font-normal placeholder:tracking-[0.05em] placeholder:text-slate-300 hover:border-slate-300 focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
                      />
                    </div>
                  </div>

                  {error && (
                    <div
                      role="alert"
                      className="login-step flex items-start gap-2.5 rounded-xl border border-rose-100 bg-rose-50/80 px-3.5 py-3 text-sm leading-6 text-rose-700"
                    >
                      <CircleAlert size={17} className="mt-0.5 shrink-0" />
                      <span>{error}</span>
                    </div>
                  )}

                  <Button type="submit" loading={loading} className="w-full">
                    دریافت کد ورود
                    <ArrowLeft size={17} />
                  </Button>

                  <p className="flex items-center justify-center gap-1.5 pt-1 text-xs text-slate-400">
                    <ShieldCheck size={14} className="text-brand-400" />
                    ورود امن با رمز یک‌بارمصرف — بدون نیاز به گذرواژه
                  </p>
                </form>
              </div>
            ) : (
              <div key="otp" className="login-step">
                <div className="mb-6">
                  <div className="mb-5 grid h-14 w-14 place-items-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-brand">
                    <ShieldCheck size={24} />
                  </div>
                  <h2 className="text-[1.6rem] font-extrabold tracking-tight text-slate-800">
                    کد تأیید
                  </h2>
                  <p className="mt-2 text-sm leading-7 text-slate-500">
                    کد ۶ رقمی ارسال‌شده به شماره{' '}
                    <span dir="ltr" className="mx-0.5 inline-block font-bold tracking-wider text-slate-700">
                      {toFa(phone)}
                    </span>{' '}
                    را وارد کنید.
                    <button
                      type="button"
                      onClick={backToPhone}
                      className="mr-2 inline-flex items-center gap-1 rounded-md text-brand-600 transition-colors hover:text-brand-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-400"
                    >
                      <Pencil size={12} />
                      ویرایش
                    </button>
                  </p>
                </div>

                <form onSubmit={verifyOtp} className="space-y-5">
                  {/* ورودی کد: یک input واقعی نامرئی + شش خانه‌ی نمایشی */}
                  <div className="relative" dir="ltr">
                    <input
                      aria-label="کد تأیید ۶ رقمی"
                      dir="ltr"
                      inputMode="numeric"
                      autoComplete="one-time-code"
                      autoFocus
                      required
                      maxLength={OTP_LEN}
                      value={code}
                      onChange={(e) => setCode(toEn(e.target.value).replace(/\D/g, '').slice(0, OTP_LEN))}
                      onFocus={() => setOtpFocused(true)}
                      onBlur={() => setOtpFocused(false)}
                      className="absolute inset-0 z-10 h-full w-full cursor-text opacity-0"
                    />
                    <div className="pointer-events-none flex justify-center gap-2 sm:gap-2.5" aria-hidden="true">
                      {Array.from({ length: OTP_LEN }).map((_, i) => {
                        const digit = codeDigits[i] ?? ''
                        const active = otpFocused && i === codeDigits.length
                        return (
                          <div
                            key={i}
                            className={cn(
                              'flex h-14 w-11 items-center justify-center rounded-2xl border-2 text-xl font-bold transition-all duration-200 sm:h-[3.75rem] sm:w-12',
                              digit
                                ? 'border-brand-300 bg-brand-50 text-brand-700 shadow-soft'
                                : 'border-slate-200 bg-white text-slate-800',
                              active && '-translate-y-0.5 border-brand-500 shadow-soft-md ring-4 ring-brand-100',
                            )}
                          >
                            {digit ? toFa(digit) : active ? (
                              <span className="login-caret h-6 w-0.5 rounded-full bg-brand-500" />
                            ) : null}
                          </div>
                        )
                      })}
                    </div>
                  </div>

                  {error && (
                    <div
                      role="alert"
                      className="login-step flex items-start gap-2.5 rounded-xl border border-rose-100 bg-rose-50/80 px-3.5 py-3 text-sm leading-6 text-rose-700"
                    >
                      <CircleAlert size={17} className="mt-0.5 shrink-0" />
                      <span>{error}</span>
                    </div>
                  )}

                  <Button type="submit" loading={loading} className="w-full">
                    ورود به سامانه
                    <ArrowLeft size={17} />
                  </Button>

                  <div className="flex items-center gap-3 py-0.5 text-xs text-slate-400">
                    <span className="h-px flex-1 bg-gradient-to-l from-transparent to-slate-200" />
                    کد را دریافت نکردید؟
                    <span className="h-px flex-1 bg-gradient-to-r from-transparent to-slate-200" />
                  </div>

                  <Button
                    type="button"
                    variant="outline"
                    loading={callLoading}
                    onClick={requestOtpByCall}
                    className="w-full"
                  >
                    <PhoneCall size={16} className="text-brand-600" />
                    دریافت کد تأیید با تماس تلفنی
                  </Button>

                  {callMsg && (
                    <div className="login-step flex items-start gap-2.5 rounded-xl border border-emerald-100 bg-emerald-50/80 px-3.5 py-3 text-sm leading-6 text-emerald-700">
                      <CircleCheck size={17} className="mt-0.5 shrink-0" />
                      <span>{callMsg}</span>
                    </div>
                  )}

                  <button
                    type="button"
                    onClick={backToPhone}
                    className="w-full rounded-lg py-1 text-center text-sm text-slate-500 transition-colors hover:text-brand-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-400"
                  >
                    ویرایش شماره موبایل
                  </button>
                </form>
              </div>
            )}
          </div>

          {/* پانوشتِ زیر کارت (نسخه‌ی موبایل، جایگزین پانوشتِ پنل برند) */}
          <p className="mt-6 text-center text-xs text-slate-400 lg:hidden">
            © آرکا — سامانه تلفن هوشمند
          </p>
        </div>
      </main>
    </div>
  )
}
