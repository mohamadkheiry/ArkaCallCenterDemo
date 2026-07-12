import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  LayoutDashboard,
  BookOpenText,
  Phone,
  Mic,
  History,
  ShieldCheck,
  Sparkles,
  PartyPopper,
  ArrowLeft,
  ArrowRight,
  X,
} from 'lucide-react'
import { Button, cn } from './ui'

export const TOUR_DONE_KEY = 'arka_tour_done'

const CARD_W = 340

interface Step {
  target?: string // مقدار data-tour
  title: string
  body: string
  icon: React.ComponentType<{ size?: number | string; className?: string }>
  adminOnly?: boolean
}

const STEPS: Step[] = [
  {
    title: 'به آرکا خوش آمدید 👋',
    body: 'در چند قدم کوتاه با بخش‌های سامانه آشنا می‌شوید. می‌توانید هر زمان با دکمه‌ی راهنما (؟) این تور را دوباره ببینید.',
    icon: PartyPopper,
  },
  {
    target: 'dashboard',
    title: 'داشبورد',
    body: 'نمای کلی وضعیت شما: شماره داخلی، دقایق مصرف‌شده و گوینده‌ی انتخابی.',
    icon: LayoutDashboard,
  },
  {
    target: 'setup',
    title: 'راه‌اندازی سریع (ویزارد)',
    body: 'ساده‌ترین راه شروع! این ویزارد قدم‌به‌قدم شما را تا ساخت تلفن هوشمند همراهی می‌کند.',
    icon: Sparkles,
  },
  {
    target: 'kb',
    title: 'پایگاه دانش',
    body: 'اطلاعات کسب‌وکارتان را اینجا وارد کنید (متن یا فایل). هوش مصنوعی بر اساس همین محتوا به تماس‌ها پاسخ می‌دهد.',
    icon: BookOpenText,
  },
  {
    target: 'smartphone',
    title: 'تلفن هوشمند',
    body: 'پیام خوش‌آمد را تنظیم کنید و تلفن هوشمند بسازید تا داخلی اختصاصی دریافت کنید.',
    icon: Phone,
  },
  {
    target: 'voice',
    title: 'صدای گوینده',
    body: 'صدایی که هوش مصنوعی با آن صحبت می‌کند را انتخاب کنید و نمونه‌ی هر گوینده را بشنوید.',
    icon: Mic,
  },
  {
    target: 'calls',
    title: 'تماس‌ها',
    body: 'تاریخچه‌ی تماس‌های پاسخ‌داده‌شده و نوع پاسخ (از پایگاه دانش یا خارج از آن) را اینجا ببینید.',
    icon: History,
  },
  {
    target: 'admin',
    title: 'پنل سوپرادمین',
    body: 'تنظیمات سراسری: OpenAI، پیامک، گوینده‌ها، دموها، پیام پذیرش، موسیقی انتظار و گزارش مصرف توکن.',
    icon: ShieldCheck,
    adminOnly: true,
  },
]

export default function Tour({
  open,
  isAdmin,
  onClose,
  onSidebarChange,
}: {
  open: boolean
  isAdmin: boolean
  onClose: () => void
  onSidebarChange?: (open: boolean) => void
}) {
  const steps = useMemo(() => STEPS.filter((s) => !s.adminOnly || isAdmin), [isAdmin])
  const [idx, setIdx] = useState(0)
  const [rect, setRect] = useState<DOMRect | null>(null)
  const navigate = useNavigate()

  const step = steps[idx]

  useEffect(() => {
    if (open) setIdx(0)
  }, [open])

  // اندازه‌گیری موقعیت عنصر هدف. در موبایل ابتدا سایدبار باز می‌شود؛ چون ترنزیشن
  // سایدبار زمان‌بر است، با چند تلاش (polling) صبر می‌کنیم تا عنصر داخل دید بیاید.
  useEffect(() => {
    if (!open || !step?.target) {
      setRect(null)
      return
    }
    const mobile = window.innerWidth < 1024
    if (mobile) onSidebarChange?.(true)

    let cancelled = false
    let tries = 0
    let timer: number | undefined
    const attempt = () => {
      if (cancelled) return
      const el = document.querySelector(`[data-tour="${step.target}"]`)
      const r = el?.getBoundingClientRect()
      const visible = r && r.width > 0 && r.left < window.innerWidth && r.right > 0
      if (visible) {
        setRect(r!)
        return
      }
      setRect(null)
      if (++tries < 10) timer = window.setTimeout(attempt, 150)
    }
    timer = window.setTimeout(attempt, mobile ? 200 : 30)
    const onResize = () => {
      tries = 0
      attempt()
    }
    window.addEventListener('resize', onResize)
    return () => {
      cancelled = true
      if (timer) clearTimeout(timer)
      window.removeEventListener('resize', onResize)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, idx, step?.target])

  if (!open || !step) return null

  function finish(goWizard = false) {
    localStorage.setItem(TOUR_DONE_KEY, '1')
    if (window.innerWidth < 1024) onSidebarChange?.(false)
    onClose()
    if (goWizard) navigate('/setup')
  }

  const isLast = idx === steps.length - 1
  const Icon = step.icon

  // موقعیت کارت:
  //  - اگر فضای کافی سمت چپِ عنصر هست (دسکتاپ): کنار عنصر.
  //  - وگرنه (موبایل): زیر یا بالای عنصر، چسبیده به لبه.
  //  - بدون عنصر هدف: وسط صفحه (با flex، بدون transform تا با انیمیشن تداخل نکند).
  let placedStyle: React.CSSProperties | null = null
  if (rect) {
    const fitsSide = rect.left >= CARD_W + 32
    if (fitsSide) {
      placedStyle = {
        position: 'fixed',
        top: Math.min(Math.max(rect.top - 8, 16), window.innerHeight - 300),
        right: window.innerWidth - rect.left + 16,
      }
    } else {
      const below = rect.bottom + 12
      placedStyle =
        below + 290 <= window.innerHeight
          ? { position: 'fixed', top: below, right: 16 }
          : { position: 'fixed', top: Math.max(16, rect.top - 302), right: 16 }
    }
  }

  const card = (
    <div
      data-tour-card
      style={placedStyle ?? undefined}
      className="w-[340px] max-w-[calc(100vw-32px)] animate-in rounded-2xl bg-white p-5 shadow-2xl"
    >
      <div className="flex items-start justify-between">
        <div className="grid h-11 w-11 place-items-center rounded-xl bg-brand-50 text-brand-600">
          <Icon size={22} />
        </div>
        <button onClick={() => finish()} className="text-slate-400 hover:text-slate-600" aria-label="بستن">
          <X size={18} />
        </button>
      </div>
      <h3 className="mt-3 text-base font-extrabold text-slate-800">{step.title}</h3>
      <p className="mt-1.5 text-sm leading-6 text-slate-500">{step.body}</p>

      {/* نقاط پیشرفت */}
      <div className="mt-4 flex items-center gap-1.5">
        {steps.map((_, i) => (
          <span
            key={i}
            className={cn('h-1.5 rounded-full transition-all', i === idx ? 'w-6 bg-brand-600' : 'w-1.5 bg-slate-200')}
          />
        ))}
      </div>

      <div className="mt-4 flex items-center justify-between gap-2">
        <button
          onClick={() => setIdx((i) => Math.max(0, i - 1))}
          disabled={idx === 0}
          className="flex items-center gap-1 rounded-lg px-3 py-2 text-sm text-slate-500 hover:bg-slate-50 disabled:opacity-40"
        >
          <ArrowRight size={16} />
          قبلی
        </button>
        {isLast ? (
          <Button onClick={() => finish(true)} className="h-10 px-4 text-xs">
            شروع راه‌اندازی سریع
            <Sparkles size={15} />
          </Button>
        ) : (
          <Button onClick={() => setIdx((i) => i + 1)} className="h-10 px-4 text-xs">
            بعدی
            <ArrowLeft size={16} />
          </Button>
        )}
      </div>
    </div>
  )

  return (
    <div className="fixed inset-0 z-50">
      {/* هایلایت عنصر هدف با حفره در پس‌زمینه‌ی تیره */}
      {rect ? (
        <div
          className="pointer-events-none fixed rounded-2xl ring-4 ring-brand-400 transition-all duration-300"
          style={{
            top: rect.top - 6,
            left: rect.left - 6,
            width: rect.width + 12,
            height: rect.height + 12,
            boxShadow: '0 0 0 9999px rgba(15,23,42,.55)',
          }}
        />
      ) : (
        <div className="fixed inset-0 bg-slate-900/55" />
      )}

      {placedStyle ? (
        card
      ) : (
        // وسط‌چین با flex (نه transform) تا با انیمیشن float-in تداخل نکند
        <div className="fixed inset-0 flex items-center justify-center p-4">{card}</div>
      )}
    </div>
  )
}
