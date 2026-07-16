import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  PlayCircle,
  BookOpenText,
  MessageSquareText,
  Mic,
  Rocket,
  Check,
  ArrowLeft,
  ArrowRight,
  CloudUpload,
  FileText,
  CircleCheck,
  PhoneCall,
} from 'lucide-react'
import { api, apiError } from '../lib/api'
import { toFa } from '../lib/format'
import { useAuth } from '../context/AuthContext'
import { Button, Card, cn } from '../components/ui'
import VoiceSampleButton from '../components/VoiceSampleButton'

const MAX_CHARS = 2000
const MAX_FILE = 100 * 1024

interface Voice {
  name: string
  displayName: string
  isDefault: boolean
  hasSample: boolean
}

const STEPS = [
  { key: 'intro', title: 'شروع', icon: PlayCircle },
  { key: 'kb', title: 'پایگاه دانش', icon: BookOpenText },
  { key: 'welcome', title: 'پیام خوش‌آمد', icon: MessageSquareText },
  { key: 'voice', title: 'گوینده', icon: Mic },
  { key: 'create', title: 'ساخت تلفن', icon: Rocket },
] as const

export default function SetupWizard() {
  const { me, refresh } = useAuth()
  const [step, setStep] = useState(0)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  // داده‌های گام‌ها
  const [videoAvailable, setVideoAvailable] = useState(false)
  const [kbMode, setKbMode] = useState<'text' | 'file'>('text')
  const [kbText, setKbText] = useState('')
  const [kbSaved, setKbSaved] = useState(false)
  const [welcome, setWelcome] = useState('')
  const [welcomeSaved, setWelcomeSaved] = useState(false)
  const [voices, setVoices] = useState<Voice[]>([])
  const [voice, setVoice] = useState('')
  const [defaultVoice, setDefaultVoice] = useState('')
  const [extension, setExtension] = useState<number | null>(null)
  const fileRef = useRef<HTMLInputElement>(null)

  // بارگذاری فقط یک‌بار در mount؛ نباید با تغییرِ me دوباره اجرا شود و ورودی‌های در حالِ ویرایشِ کاربر را بازنویسی کند.
  useEffect(() => {
    api.get('/api/tutorial-video/info').then(({ data }) => setVideoAvailable(!!data.available)).catch(() => {})
    api.get('/api/knowledge-base').then(({ data }) => {
      if (data) {
        setKbSaved(data.moderationStatus === 'Approved')
        if (data.sourceType === 'Text' && data.rawText) setKbText(data.rawText)
      }
    })
    api.get('/api/smartphone').then(({ data }) => {
      if (data?.welcomeMessageText) {
        setWelcome(data.welcomeMessageText)
        setWelcomeSaved(true)
      }
      if (data?.extension && data?.status === 'Active') setExtension(data.extension)
    })
    api.get<{ voices: Voice[]; defaultVoice: string }>('/api/voices').then(({ data }) => {
      setVoices(data.voices)
      setDefaultVoice(data.defaultVoice)
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // مقداردهیِ اولیه‌ی گوینده وقتی me/لیست آماده شد — فقط اگر کاربر هنوز چیزی انتخاب نکرده.
  useEffect(() => {
    setVoice((v) => v || me?.voiceName || defaultVoice)
  }, [me?.voiceName, defaultVoice])

  function next() {
    setError('')
    setStep((s) => Math.min(STEPS.length - 1, s + 1))
  }
  function back() {
    setError('')
    setStep((s) => Math.max(0, s - 1))
  }

  async function saveKbText() {
    if (!kbText.trim()) return setError('متن پایگاه دانش را وارد کنید.')
    if (kbText.length > MAX_CHARS) return setError(`حداکثر ${toFa(MAX_CHARS)} کاراکتر مجاز است.`)
    setBusy(true)
    setError('')
    try {
      await api.post('/api/knowledge-base/text', { text: kbText })
      setKbSaved(true)
      next()
    } catch (e) {
      setError(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  async function saveKbFile(file: File) {
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'))
    if (!['.txt', '.docx'].includes(ext)) return setError('فقط فایل txt و Word (docx) مجاز است.')
    if (file.size > MAX_FILE) return setError('حجم فایل باید حداکثر ۱۰۰ کیلوبایت باشد.')
    setBusy(true)
    setError('')
    try {
      const form = new FormData()
      form.append('file', file)
      await api.post('/api/knowledge-base/file', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      setKbSaved(true)
      next()
    } catch (e) {
      setError(apiError(e))
    } finally {
      setBusy(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  async function saveWelcome() {
    if (!welcome.trim()) return setError('متن پیام خوش‌آمد را وارد کنید.')
    setBusy(true)
    setError('')
    try {
      await api.put('/api/smartphone/welcome', { text: welcome })
      setWelcomeSaved(true)
      next()
    } catch (e) {
      setError(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  async function saveVoice() {
    setBusy(true)
    setError('')
    try {
      if (voice) await api.put('/api/me/voice', { voiceName: voice })
      next()
    } catch (e) {
      setError(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  async function create() {
    setBusy(true)
    setError('')
    try {
      const { data } = await api.post('/api/smartphone')
      setExtension(data.extension)
      await refresh()
    } catch (e) {
      setError(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">راه‌اندازی سریع</h1>
        <p className="mt-1 text-sm text-slate-500">در چند قدم ساده تلفن هوشمند خود را بسازید.</p>
      </div>

      {/* استپر */}
      <div className="flex items-center justify-between">
        {STEPS.map((s, i) => {
          const Icon = s.icon
          const done = i < step || (i === STEPS.length - 1 && extension != null)
          const active = i === step
          return (
            <div key={s.key} className="flex flex-1 items-center last:flex-none">
              <button
                onClick={() => i < step && setStep(i)}
                className="flex flex-col items-center gap-1.5"
                disabled={i > step}
              >
                <span
                  className={cn(
                    'grid h-11 w-11 place-items-center rounded-2xl border-2 transition-all',
                    done
                      ? 'border-emerald-500 bg-emerald-500 text-white'
                      : active
                        ? 'border-brand-500 bg-brand-50 text-brand-600 ring-4 ring-brand-100'
                        : 'border-slate-200 bg-white text-slate-300',
                  )}
                >
                  {done ? <Check size={20} /> : <Icon size={20} />}
                </span>
                <span className={cn('text-[11px] font-medium', active ? 'text-brand-700' : done ? 'text-emerald-600' : 'text-slate-400')}>
                  {s.title}
                </span>
              </button>
              {i < STEPS.length - 1 && (
                <div className={cn('mx-2 mb-5 h-0.5 flex-1 rounded-full', i < step ? 'bg-emerald-400' : 'bg-slate-200')} />
              )}
            </div>
          )
        })}
      </div>

      <Card className="animate-in">
        {/* گام ۰: شروع + ویدیو آموزشی */}
        {step === 0 && (
          <div className="space-y-4">
            <h3 className="text-lg font-bold text-slate-800">به سامانه تلفن هوشمند آرکا خوش آمدید</h3>
            <p className="text-sm leading-7 text-slate-500">
              در این ویزارد: پایگاه دانش کسب‌وکارتان را وارد می‌کنید، پیام خوش‌آمد و گوینده را انتخاب می‌کنید،
              و در پایان تلفن هوشمند شما با یک داخلی اختصاصی ساخته می‌شود.
            </p>
            {videoAvailable && (
              <div className="overflow-hidden rounded-2xl border border-slate-200">
                <div className="flex items-center gap-2 border-b border-slate-100 bg-slate-50/60 px-4 py-2.5 text-sm font-medium text-slate-700">
                  <PlayCircle size={17} className="text-brand-600" />
                  ویدیوی آموزشی
                </div>
                <video src="/api/tutorial-video" controls className="w-full" preload="metadata" />
              </div>
            )}
            <div className="flex justify-end">
              <Button onClick={next}>
                شروع کنیم
                <ArrowLeft size={17} />
              </Button>
            </div>
          </div>
        )}

        {/* گام ۱: پایگاه دانش */}
        {step === 1 && (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-bold text-slate-800">پایگاه دانش</h3>
              {kbSaved && (
                <span className="flex items-center gap-1 rounded-full bg-emerald-50 px-3 py-1 text-xs text-emerald-700">
                  <CircleCheck size={14} /> ثبت‌شده
                </span>
              )}
            </div>
            <p className="text-sm text-slate-500">
              اطلاعات کسب‌وکار، خدمات، ساعات کاری و سوالات پرتکرار. حداکثر {toFa(MAX_CHARS)} کاراکتر متن یا فایل {toFa(100)} کیلوبایتی.
            </p>
            <div className="inline-flex rounded-xl bg-slate-100 p-1">
              {(['text', 'file'] as const).map((m) => (
                <button
                  key={m}
                  onClick={() => setKbMode(m)}
                  className={cn(
                    'flex items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-medium transition-colors',
                    kbMode === m ? 'bg-white text-brand-700 shadow-sm' : 'text-slate-500',
                  )}
                >
                  {m === 'text' ? <FileText size={15} /> : <CloudUpload size={15} />}
                  {m === 'text' ? 'ورود متن' : 'بارگذاری فایل'}
                </button>
              ))}
            </div>

            {kbMode === 'text' ? (
              <>
                <textarea
                  value={kbText}
                  onChange={(e) => setKbText(e.target.value)}
                  rows={7}
                  maxLength={MAX_CHARS}
                  placeholder="مثال: فروشگاه ما از ساعت ۹ تا ۱۸ باز است. ارسال رایگان برای خرید بالای ..."
                  className="w-full resize-none rounded-xl border border-slate-200 p-4 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
                />
                <div className="flex items-center justify-between">
                  <span className="text-xs text-slate-400">
                    {toFa(kbText.length)} / {toFa(MAX_CHARS)}
                  </span>
                  <div className="flex gap-2">
                    <Button variant="ghost" onClick={back}>
                      <ArrowRight size={16} /> قبلی
                    </Button>
                    {kbSaved && !kbText.trim() ? (
                      <Button variant="outline" onClick={next}>رد شدن (ثبت‌شده)</Button>
                    ) : (
                      <Button onClick={saveKbText} loading={busy}>
                        ذخیره و ادامه <ArrowLeft size={16} />
                      </Button>
                    )}
                  </div>
                </div>
              </>
            ) : (
              <>
                <label
                  className="flex cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/50 p-10 text-center transition-colors hover:border-brand-300 hover:bg-brand-50/40"
                  onDrop={(e) => {
                    e.preventDefault()
                    if (e.dataTransfer.files[0]) saveKbFile(e.dataTransfer.files[0])
                  }}
                  onDragOver={(e) => e.preventDefault()}
                >
                  <CloudUpload size={32} className="text-brand-500" />
                  <span className="text-sm font-medium text-slate-700">فایل را اینجا رها کنید یا کلیک کنید</span>
                  <span className="text-xs text-slate-400">txt یا Word (docx) · حداکثر ۱۰۰ کیلوبایت</span>
                  <input
                    ref={fileRef}
                    type="file"
                    accept=".txt,.docx"
                    className="hidden"
                    onChange={(e) => e.target.files?.[0] && saveKbFile(e.target.files[0])}
                  />
                </label>
                <div className="flex justify-between">
                  <Button variant="ghost" onClick={back}>
                    <ArrowRight size={16} /> قبلی
                  </Button>
                  {kbSaved && <Button variant="outline" onClick={next}>ادامه (ثبت‌شده)</Button>}
                </div>
              </>
            )}
          </div>
        )}

        {/* گام ۲: پیام خوش‌آمد */}
        {step === 2 && (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-bold text-slate-800">پیام خوش‌آمد</h3>
              {welcomeSaved && (
                <span className="flex items-center gap-1 rounded-full bg-emerald-50 px-3 py-1 text-xs text-emerald-700">
                  <CircleCheck size={14} /> ثبت‌شده
                </span>
              )}
            </div>
            <p className="text-sm text-slate-500">این پیام در ابتدای هر تماس برای تماس‌گیرنده پخش می‌شود.</p>
            <textarea
              value={welcome}
              onChange={(e) => setWelcome(e.target.value)}
              rows={3}
              placeholder="سلام، به فروشگاه ما خوش آمدید. لطفاً سوال خود را بفرمایید."
              className="w-full resize-none rounded-xl border border-slate-200 p-4 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
            />
            <div className="flex justify-between">
              <Button variant="ghost" onClick={back}>
                <ArrowRight size={16} /> قبلی
              </Button>
              <Button onClick={saveWelcome} loading={busy}>
                ذخیره و ادامه <ArrowLeft size={16} />
              </Button>
            </div>
          </div>
        )}

        {/* گام ۳: گوینده */}
        {step === 3 && (
          <div className="space-y-4">
            <h3 className="text-lg font-bold text-slate-800">صدای گوینده</h3>
            <p className="text-sm text-slate-500">هوش مصنوعی با این صدا به تماس‌گیرندگان پاسخ می‌دهد.</p>
            <div className="grid gap-3 sm:grid-cols-2">
              {voices.map((v) => (
                <button
                  key={v.name}
                  onClick={() => setVoice(v.name)}
                  className={cn(
                    'flex items-center justify-between rounded-2xl border p-4 text-right transition-all',
                    voice === v.name
                      ? 'border-brand-400 bg-brand-50 ring-4 ring-brand-100'
                      : 'border-slate-200 bg-white hover:border-slate-300',
                  )}
                >
                  <div className="flex items-center gap-3">
                    <span className="grid h-10 w-10 place-items-center rounded-xl bg-white text-brand-600 shadow-sm">
                      <Mic size={18} />
                    </span>
                    <div>
                      <div className="text-sm font-semibold text-slate-800">{v.displayName}</div>
                      <div className="text-xs text-slate-400" dir="ltr">{v.name}</div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <VoiceSampleButton voiceName={v.name} hasSample={v.hasSample} />
                    <span
                      className={cn(
                        'grid h-6 w-6 place-items-center rounded-full border',
                        voice === v.name ? 'border-brand-500 bg-brand-600 text-white' : 'border-slate-300 text-transparent',
                      )}
                    >
                      <Check size={13} />
                    </span>
                  </div>
                </button>
              ))}
            </div>
            <div className="flex justify-between">
              <Button variant="ghost" onClick={back}>
                <ArrowRight size={16} /> قبلی
              </Button>
              <Button onClick={saveVoice} loading={busy}>
                ذخیره و ادامه <ArrowLeft size={16} />
              </Button>
            </div>
          </div>
        )}

        {/* گام ۴: ساخت */}
        {step === 4 && (
          <div className="space-y-5">
            {extension == null ? (
              <>
                <h3 className="text-lg font-bold text-slate-800">ساخت تلفن هوشمند</h3>
                <p className="text-sm leading-7 text-slate-500">
                  همه‌چیز آماده است! با کلیک روی دکمه‌ی زیر، یک داخلی اختصاصی برای شما ساخته می‌شود و
                  تلفن هوشمندتان روی آن فعال می‌گردد.
                </p>
                <div className="space-y-2.5">
                  {[
                    { ok: kbSaved, label: 'پایگاه دانش تأییدشده' },
                    { ok: welcomeSaved, label: 'پیام خوش‌آمد ثبت‌شده' },
                    { ok: !!voice, label: 'گوینده انتخاب‌شده' },
                  ].map((c) => (
                    <div key={c.label} className="flex items-center gap-2.5 text-sm">
                      <span
                        className={cn(
                          'grid h-6 w-6 place-items-center rounded-full',
                          c.ok ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-400',
                        )}
                      >
                        <Check size={13} />
                      </span>
                      <span className={c.ok ? 'text-slate-700' : 'text-slate-400'}>{c.label}</span>
                    </div>
                  ))}
                </div>
                <div className="flex justify-between">
                  <Button variant="ghost" onClick={back}>
                    <ArrowRight size={16} /> قبلی
                  </Button>
                  <Button onClick={create} loading={busy} disabled={!kbSaved || !welcomeSaved}>
                    <Rocket size={17} />
                    ایجاد تلفن هوشمند
                  </Button>
                </div>
              </>
            ) : (
              <div className="py-6 text-center">
                <div className="mx-auto grid h-16 w-16 place-items-center rounded-3xl bg-emerald-100 text-emerald-600">
                  <PhoneCall size={30} />
                </div>
                <h3 className="mt-4 text-xl font-extrabold text-slate-800">تلفن هوشمند شما آماده است 🎉</h3>
                <p className="mt-1 text-sm text-slate-500">شماره داخلی اختصاصی شما:</p>
                <div className="mt-3 text-4xl font-extrabold tracking-widest text-emerald-600">{toFa(extension)}</div>

                <div className="mx-auto mt-6 max-w-md rounded-2xl border border-emerald-200 bg-emerald-50/60 p-4 text-right">
                  <p className="font-bold text-slate-800">چطور با تلفن هوشمند خود تماس بگیرید؟</p>
                  <p className="mt-1 text-sm leading-7 text-slate-600">
                    ابتدا با شماره‌ی{' '}
                    <span dir="ltr" className="font-bold text-emerald-700">{toFa(me?.receptionNumber ?? '02191008288')}</span>{' '}
                    تماس بگیرید، سپس پس از پخش پیام پذیرش، شماره داخلی اختصاصی خود{' '}
                    <span dir="ltr" className="font-bold text-emerald-700">{toFa(extension)}</span>{' '}
                    را شماره‌گیری کنید.
                  </p>
                </div>

                <div className="mt-6 flex justify-center gap-3">
                  <Link to="/">
                    <Button variant="outline">رفتن به داشبورد</Button>
                  </Link>
                  <Link to="/calls">
                    <Button>مشاهده تماس‌ها</Button>
                  </Link>
                </div>
              </div>
            )}
          </div>
        )}

        {error && <p className="mt-4 rounded-xl bg-rose-50 px-4 py-3 text-sm text-rose-700">{error}</p>}
      </Card>
    </div>
  )
}
