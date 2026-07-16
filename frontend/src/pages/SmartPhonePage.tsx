import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Check as CheckIcon, Dot, PhoneCall } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { toFa } from '../lib/format'
import { useAuth } from '../context/AuthContext'
import { Button, Card, cn } from '../components/ui'

interface Sp {
  extension: number | null
  status: string
  welcomeMessageText?: string | null
  hasWelcomeAudio: boolean
  answerAccuracyPercent?: number
}

function Check({ ok, children }: { ok: boolean; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-3">
      <span
        className={cn(
          'grid h-6 w-6 place-items-center rounded-full',
          ok ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-400',
        )}
      >
        {ok ? <CheckIcon size={13} /> : <Dot size={16} />}
      </span>
      <span className={cn('text-sm', ok ? 'text-slate-700' : 'text-slate-400')}>{children}</span>
    </div>
  )
}

export default function SmartPhonePage() {
  const { refresh } = useAuth()
  const [sp, setSp] = useState<Sp | null>(null)
  const [welcome, setWelcome] = useState('')
  const [accuracy, setAccuracy] = useState(70)
  const [savingAcc, setSavingAcc] = useState(false)
  const [hasKb, setHasKb] = useState(false)
  const [busy, setBusy] = useState(false)
  const [creating, setCreating] = useState(false)
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  async function load() {
    const [s, kb] = await Promise.all([
      api.get<Sp | null>('/api/smartphone'),
      api.get<{ moderationStatus: string } | null>('/api/knowledge-base'),
    ])
    setSp(s.data)
    if (s.data?.welcomeMessageText) setWelcome(s.data.welcomeMessageText)
    if (s.data?.answerAccuracyPercent) setAccuracy(s.data.answerAccuracyPercent)
    setHasKb(!!kb.data && kb.data.moderationStatus === 'Approved')
  }
  useEffect(() => {
    load()
  }, [])

  async function saveWelcome() {
    setBusy(true)
    setMsg(null)
    try {
      await api.put('/api/smartphone/welcome', { text: welcome })
      setMsg({ type: 'ok', text: 'پیام خوش‌آمد ذخیره و صوت آن تولید شد.' })
      await load()
    } catch (e) {
      setMsg({ type: 'err', text: apiError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function saveAccuracy() {
    setSavingAcc(true)
    setMsg(null)
    try {
      await api.put('/api/smartphone/accuracy', { percent: accuracy })
      setMsg({ type: 'ok', text: 'دقت پاسخ‌ها ذخیره شد.' })
    } catch (e) {
      setMsg({ type: 'err', text: apiError(e) })
    } finally {
      setSavingAcc(false)
    }
  }

  async function create() {
    setCreating(true)
    setMsg(null)
    try {
      const { data } = await api.post('/api/smartphone')
      setMsg({ type: 'ok', text: `تلفن هوشمند ساخته شد. داخلی شما: ${toFa(data.extension)}` })
      await load()
      await refresh()
    } catch (e) {
      setMsg({ type: 'err', text: apiError(e) })
    } finally {
      setCreating(false)
    }
  }

  const isActive = sp?.status === 'Active' && sp.extension != null
  const hasWelcome = !!sp?.welcomeMessageText
  const canCreate = hasKb && hasWelcome && !isActive

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">تلفن هوشمند</h1>
        <p className="mt-1 text-sm text-slate-500">پیام خوش‌آمد را ثبت کنید و تلفن هوشمند خود را بسازید.</p>
      </div>

      {isActive && (
        <Card className="animate-in border-emerald-200 bg-gradient-to-l from-emerald-50 to-white">
          <div className="flex items-center gap-4">
            <span className="grid h-14 w-14 place-items-center rounded-2xl bg-emerald-100 text-emerald-600">
              <PhoneCall size={26} />
            </span>
            <div>
              <div className="text-sm text-slate-500">داخلی اختصاصی شما</div>
              <div className="text-3xl font-extrabold tracking-wider text-emerald-700">{toFa(sp!.extension!)}</div>
            </div>
          </div>
        </Card>
      )}

      <Card className="animate-in">
        <h3 className="font-bold text-slate-800">پیام خوش‌آمد</h3>
        <p className="mt-1 text-sm text-slate-500">
          این پیام هنگام تماس، ابتدا برای تماس‌گیرنده پخش می‌شود.
        </p>
        <textarea
          rows={3}
          value={welcome}
          onChange={(e) => setWelcome(e.target.value)}
          placeholder="سلام، به مجموعه‌ی ما خوش آمدید. لطفاً سوال خود را بفرمایید."
          className="mt-4 w-full resize-none rounded-xl border border-slate-200 p-4 text-sm outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
        />
        <div className="mt-3">
          <Button onClick={saveWelcome} loading={busy} variant="outline">
            ذخیره پیام خوش‌آمد
          </Button>
        </div>
      </Card>

      <Card className="animate-in">
        <h3 className="font-bold text-slate-800">دقتِ پاسخ‌ها بر اساس پایگاه دانش</h3>
        <p className="mt-1 text-sm leading-7 text-slate-500">
          تعیین می‌کند پاسخ‌های هوش مصنوعی چقدر به متنِ پایگاه دانشِ شما پایبند باشند.
          <b className="text-slate-700"> هرچه بالاتر</b>، پاسخ‌ها دقیق‌تر، مطمئن‌تر و نزدیک‌تر به پایگاه دانش
          هستند (خلاقیتِ کمتر). <b className="text-slate-700">هرچه پایین‌تر</b>، پاسخ‌ها آزادتر و خلاقانه‌تر
          می‌شوند (اما ممکن است از پایگاه دانش فاصله بگیرند). برای پاسخ‌گوییِ رسمی و دقیق، مقدارِ بالا
          (حدود ۸۰ تا ۱۰۰ درصد) پیشنهاد می‌شود.
        </p>
        <div className="mt-5 flex items-center gap-4">
          <input
            type="range"
            min={10}
            max={100}
            step={5}
            value={accuracy}
            onChange={(e) => setAccuracy(Number(e.target.value))}
            className="h-2 flex-1 cursor-pointer accent-brand-600"
          />
          <span className="w-16 text-center text-lg font-extrabold text-brand-700">{toFa(accuracy)}٪</span>
        </div>
        <div className="mt-1 flex justify-between text-xs text-slate-400">
          <span>خلاقانه‌تر (۱۰٪)</span>
          <span>دقیق‌تر بر پایه‌ی پایگاه دانش (۱۰۰٪)</span>
        </div>
        <div className="mt-4">
          <Button onClick={saveAccuracy} loading={savingAcc} variant="outline">
            ذخیره دقت پاسخ‌ها
          </Button>
        </div>
      </Card>

      <Card className="animate-in">
        <h3 className="font-bold text-slate-800">پیش‌نیازهای ساخت</h3>
        <div className="mt-4 space-y-3">
          <Check ok={hasKb}>
            پایگاه دانش تأییدشده{' '}
            {!hasKb && (
              <Link to="/knowledge-base" className="text-brand-600 hover:underline">
                (ثبت پایگاه دانش)
              </Link>
            )}
          </Check>
          <Check ok={hasWelcome}>پیام خوش‌آمد ثبت‌شده</Check>
        </div>

        <div className="mt-5 flex items-center gap-4">
          <Button onClick={create} loading={creating} disabled={!canCreate}>
            {isActive ? 'تلفن هوشمند فعال است' : 'ایجاد تلفن هوشمند'}
          </Button>
          {!canCreate && !isActive && (
            <span className="text-xs text-slate-400">ابتدا پیش‌نیازها را کامل کنید.</span>
          )}
        </div>
      </Card>

      {msg && (
        <div
          className={cn(
            'rounded-xl px-4 py-3 text-sm',
            msg.type === 'ok' ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700',
          )}
        >
          {msg.text}
        </div>
      )}
    </div>
  )
}
