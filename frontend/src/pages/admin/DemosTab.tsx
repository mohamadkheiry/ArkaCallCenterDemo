import { useEffect, useRef, useState } from 'react'
import { ChevronDown, FlaskConical, Plus, PlayCircle, RefreshCw, Save, Trash2, Video } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { Button, Card, Skeleton, TextInput, cn } from '../../components/ui'
import { toFa } from '../../lib/format'
import AudioPlayButton from '../../components/AudioPlayButton'

interface Demo {
  id: number
  label?: string | null
  extension?: number | null
  status: string
  welcomeText?: string | null
  kbText?: string | null
  voiceName?: string | null
  callMinuteLimit?: number | null
  usedMinutes: number
  isActive: boolean
}
interface Voice {
  name: string
  displayName: string
}

interface KnowledgeAnswer {
  id: number
  question: string
  answer: string
  audioStatus: 'Pending' | 'Ready' | 'Failed'
  audioError?: string | null
  updatedAt: string
}

function DemoKnowledgeEditor({ demoId }: { demoId: number }) {
  const [open, setOpen] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [items, setItems] = useState<KnowledgeAnswer[]>([])
  const [total, setTotal] = useState(0)
  const [question, setQuestion] = useState('')
  const [answer, setAnswer] = useState('')
  const [fallback, setFallback] = useState('')
  const [fallbackReady, setFallbackReady] = useState(false)
  const [fallbackUpdatedAt, setFallbackUpdatedAt] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')

  async function load(reset = true) {
    const skip = reset ? 0 : items.length
    const [answers, fallbackResponse] = await Promise.all([
      api.get<{ total: number; items: KnowledgeAnswer[] }>(`/api/admin/demos/${demoId}/knowledge-answers`, { params: { skip, take: 100 } }),
      api.get<{ text?: string | null; audioReady: boolean; updatedAt?: string | null }>(`/api/admin/demos/${demoId}/knowledge-fallback`),
    ])
    setItems((current) => reset ? answers.data.items : [...current, ...answers.data.items])
    setTotal(answers.data.total)
    setFallback(fallbackResponse.data.text ?? '')
    setFallbackReady(fallbackResponse.data.audioReady)
    setFallbackUpdatedAt(fallbackResponse.data.updatedAt ?? null)
    setLoaded(true)
  }

  async function toggle() {
    setOpen((value) => !value)
    if (!loaded) {
      setBusy(true)
      try { await load() } finally { setBusy(false) }
    }
  }

  async function add() {
    if (!question.trim() || !answer.trim()) return setMsg('سؤال و پاسخ را کامل کنید.')
    setBusy(true); setMsg('')
    try {
      await api.post(`/api/admin/demos/${demoId}/knowledge-answers`, { question, answer })
      setQuestion(''); setAnswer(''); setMsg('سؤال و صوت پاسخ ذخیره شد.'); await load(true)
    } catch (e) { setMsg(apiError(e)) } finally { setBusy(false) }
  }

  async function update(item: KnowledgeAnswer) {
    setBusy(true); setMsg('')
    try {
      await api.put(`/api/admin/demos/${demoId}/knowledge-answers/${item.id}`, { question: item.question, answer: item.answer })
      setMsg('تغییرات و صوت جدید ذخیره شدند.'); await load()
    } catch (e) { setMsg(apiError(e)) } finally { setBusy(false) }
  }

  async function remove(id: number) {
    if (!confirm('این سؤال و پاسخ حذف شود؟')) return
    setBusy(true)
    try { await api.delete(`/api/admin/demos/${demoId}/knowledge-answers/${id}`); await load() }
    catch (e) { setMsg(apiError(e)) } finally { setBusy(false) }
  }

  async function regenerate(id: number) {
    setBusy(true); setMsg('')
    try {
      await api.post(`/api/admin/demos/${demoId}/knowledge-answers/${id}/regenerate-audio`)
      setMsg('صوت پاسخ دوباره تولید شد.'); await load()
    } catch (e) { setMsg(apiError(e)) } finally { setBusy(false) }
  }

  async function saveFallback() {
    if (!fallback.trim()) return setMsg('پیام جایگزین نمی‌تواند خالی باشد.')
    setBusy(true); setMsg('')
    try {
      const { data } = await api.put<{ audioReady: boolean; updatedAt?: string | null }>(`/api/admin/demos/${demoId}/knowledge-fallback`, { text: fallback })
      setFallbackReady(data.audioReady)
      setFallbackUpdatedAt(data.updatedAt ?? new Date().toISOString())
      setMsg('پیام جایگزین و صوت آن ذخیره شدند.')
    } catch (e) { setMsg(apiError(e)) } finally { setBusy(false) }
  }

  return (
    <div className="mt-4 border-t border-slate-100 pt-4">
      <button type="button" onClick={toggle} className="flex w-full items-center gap-2 rounded-xl bg-slate-50 px-4 py-3 text-right text-sm font-bold text-slate-700">
        پایگاه دانش سؤال و جواب <span className="text-xs font-normal text-slate-400">({toFa(total)} مورد)</span>
        <ChevronDown size={16} className={cn('mr-auto transition-transform', open && 'rotate-180')} />
      </button>
      {open && (
        <div className="mt-3 space-y-3">
          <div className="grid gap-2 rounded-xl border border-brand-100 bg-brand-50/30 p-3 sm:grid-cols-2">
            <textarea rows={3} maxLength={500} value={question} onChange={(e) => setQuestion(e.target.value)} placeholder="سؤال" className="rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400" />
            <textarea rows={3} maxLength={4000} value={answer} onChange={(e) => setAnswer(e.target.value)} placeholder="پاسخ قابل پخش" className="rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400" />
            <Button className="h-10 sm:col-span-2" onClick={add} loading={busy}><Plus size={15} /> افزودن و تولید صوت</Button>
          </div>
          {items.map((item, index) => (
            <div key={item.id} className="rounded-xl border border-slate-200 p-3">
              <div className="mb-2 text-xs font-bold text-slate-400">سؤال {toFa(index + 1)}</div>
              <div className="grid gap-2 sm:grid-cols-2">
                <textarea rows={3} value={item.question} onChange={(e) => setItems((all) => all.map((x) => x.id === item.id ? { ...x, question: e.target.value } : x))} className="rounded-xl border border-slate-200 p-3 text-sm" />
                <textarea rows={3} value={item.answer} onChange={(e) => setItems((all) => all.map((x) => x.id === item.id ? { ...x, answer: e.target.value } : x))} className="rounded-xl border border-slate-200 p-3 text-sm" />
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                {item.audioStatus === 'Ready' && <AudioPlayButton path={`/api/admin/demos/${demoId}/knowledge-answers/${item.id}/audio?v=${encodeURIComponent(item.updatedAt)}`} />}
                <Button className="h-9 px-3 text-xs" variant="outline" onClick={() => update(item)} loading={busy}><Save size={14} /> ذخیره</Button>
                <Button className="h-9 px-3 text-xs" variant="outline" onClick={() => regenerate(item.id)} loading={busy}><RefreshCw size={14} /> بازتولید صوت</Button>
                <Button className="h-9 px-3 text-xs" variant="danger" onClick={() => remove(item.id)} loading={busy}><Trash2 size={14} /> حذف</Button>
                {item.audioStatus !== 'Ready' && <span className="text-xs text-rose-600">{item.audioError || 'صوت آماده نیست'}</span>}
              </div>
            </div>
          ))}
          {total > items.length && <div className="flex justify-center"><Button className="h-9 px-4 text-xs" variant="outline" onClick={() => load(false)} loading={busy}>نمایش موارد بیشتر</Button></div>}
          <div className="rounded-xl border border-slate-200 p-3">
            <div className="mb-1.5 text-xs font-bold text-slate-600">پیام سؤال بی‌پاسخ</div>
            <textarea rows={2} maxLength={1500} value={fallback} onChange={(e) => setFallback(e.target.value)} className="w-full rounded-xl border border-slate-200 p-3 text-sm" />
            <div className="mt-2 flex flex-wrap items-center gap-2">
              {fallbackReady && <AudioPlayButton path={`/api/admin/demos/${demoId}/knowledge-fallback/audio?v=${encodeURIComponent(fallbackUpdatedAt ?? 'current')}`} />}
              <Button className="h-9 px-3 text-xs" variant="outline" onClick={saveFallback} loading={busy}>ذخیره و تولید صوت</Button>
            </div>
          </div>
          {msg && <p className={cn('rounded-xl px-3 py-2 text-xs', msg.includes('ذخیره') ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700')}>{msg}</p>}
        </div>
      )}
    </div>
  )
}

function VoiceSelect({ value, onChange, voices }: { value: string; onChange: (v: string) => void; voices: Voice[] }) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="h-12 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm outline-none focus:border-brand-400"
    >
      <option value="">پیش‌فرض</option>
      {voices.map((v) => (
        <option key={v.name} value={v.name}>
          {v.displayName} ({v.name})
        </option>
      ))}
    </select>
  )
}

function DemoRow({ demo, voices, onChanged }: { demo: Demo; voices: Voice[]; onChanged: () => void }) {
  const [d, setD] = useState(demo)
  const [busy, setBusy] = useState(false)

  async function save() {
    setBusy(true)
    try {
      await api.put(`/api/admin/demos/${demo.id}`, {
        label: d.label,
        welcomeText: d.welcomeText,
        kbText: d.kbText,
        voice: d.voiceName,
        minuteLimit: d.callMinuteLimit,
        isActive: d.isActive,
      })
      onChanged()
    } finally {
      setBusy(false)
    }
  }
  async function remove() {
    if (!confirm(`دموی «${demo.label}» حذف شود؟`)) return
    setBusy(true)
    try {
      await api.delete(`/api/admin/demos/${demo.id}`)
      onChanged()
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rounded-2xl border border-slate-200 p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-50 text-brand-600">
            <FlaskConical size={19} />
          </span>
          <div>
            <div className="text-sm font-bold text-slate-800">{demo.label}</div>
            <div className="text-xs text-slate-400">
              داخلی {demo.extension != null ? toFa(demo.extension) : '—'} · {demo.status} · مصرف {toFa(demo.usedMinutes)} دقیقه
            </div>
          </div>
        </div>
        <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-600">
          <input type="checkbox" checked={d.isActive} onChange={(e) => setD({ ...d, isActive: e.target.checked })} />
          فعال
        </label>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <TextInput label="نام دمو" value={d.label ?? ''} onChange={(e) => setD({ ...d, label: e.target.value })} />
        <TextInput
          label="محدودیت مکالمه (دقیقه)"
          type="number"
          value={d.callMinuteLimit ?? ''}
          onChange={(e) => setD({ ...d, callMinuteLimit: e.target.value === '' ? null : Number(e.target.value) })}
        />
        <div>
          <span className="mb-1.5 block text-sm font-medium text-slate-700">گوینده</span>
          <VoiceSelect value={d.voiceName ?? ''} onChange={(v) => setD({ ...d, voiceName: v })} voices={voices} />
        </div>
        <div className="sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-slate-700">پیام خوش‌آمد</span>
          <textarea
            rows={2}
            value={d.welcomeText ?? ''}
            onChange={(e) => setD({ ...d, welcomeText: e.target.value })}
            className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400"
          />
        </div>
        {d.kbText && <div className="sm:col-span-2 rounded-xl bg-amber-50 px-3 py-2 text-xs leading-6 text-amber-800">پایگاه دانش متنی قبلی حفظ شده است. تماس جدید فقط از سؤال‌وجواب‌های پایین این کارت استفاده می‌کند.</div>}
      </div>

      <div className="mt-3 flex gap-3">
        <Button onClick={save} loading={busy}>
          ذخیره
        </Button>
        <Button variant="danger" onClick={remove} loading={busy}>
          حذف
        </Button>
      </div>
      <DemoKnowledgeEditor demoId={demo.id} />
    </div>
  )
}

function TutorialVideoCard() {
  const [available, setAvailable] = useState(false)
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)
  const ref = useRef<HTMLInputElement>(null)

  useEffect(() => {
    api.get('/api/tutorial-video/info').then(({ data }) => setAvailable(!!data.available))
  }, [refreshKey])

  async function upload(file: File) {
    setMsg('')
    const ok = ['.mp4', '.webm'].some((e) => file.name.toLowerCase().endsWith(e))
    if (!ok) return setMsg('فقط فرمت mp4 یا webm مجاز است.')
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/api/admin/tutorial-video', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      setMsg(data.message)
      setRefreshKey((k) => k + 1)
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setBusy(false)
      if (ref.current) ref.current.value = ''
    }
  }

  async function remove() {
    if (!confirm('ویدیوی آموزشی حذف شود؟')) return
    setBusy(true)
    try {
      await api.delete('/api/admin/tutorial-video')
      setMsg('ویدیو حذف شد.')
      setRefreshKey((k) => k + 1)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="animate-in">
      <div className="flex items-center gap-2">
        <Video size={19} className="text-brand-600" />
        <h3 className="text-lg font-bold text-slate-800">ویدیوی آموزشی</h3>
      </div>
      <p className="mt-1 text-sm text-slate-500">
        این ویدیو در داشبورد کاربران و ابتدای ویزارد راه‌اندازی نمایش داده می‌شود (آموزش ساخت دمو / کار با سامانه).
      </p>
      <div className="mt-4 space-y-4">
        {available && (
          <video key={refreshKey} src={`/api/tutorial-video?v=${refreshKey}`} controls className="w-full rounded-xl" preload="metadata" />
        )}
        <div className="flex flex-wrap items-center gap-3">
          <label className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:border-brand-300 hover:text-brand-700">
            <PlayCircle size={17} />
            {busy ? 'در حال بارگذاری…' : available ? 'جایگزینی ویدیو' : 'بارگذاری ویدیو (mp4/webm)'}
            <input
              ref={ref}
              type="file"
              accept=".mp4,.webm"
              className="hidden"
              disabled={busy}
              onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])}
            />
          </label>
          {available && (
            <Button variant="danger" onClick={remove} loading={busy} className="h-10 px-4 text-xs">
              <Trash2 size={15} />
              حذف ویدیو
            </Button>
          )}
          {msg && <span className="text-sm text-emerald-600">{msg}</span>}
        </div>
      </div>
    </Card>
  )
}

export default function DemosTab() {
  const [demos, setDemos] = useState<Demo[]>([])
  const [voices, setVoices] = useState<Voice[]>([])
  const [creating, setCreating] = useState(false)
  const [loading, setLoading] = useState(true)
  const [msg, setMsg] = useState('')
  const [form, setForm] = useState({ extension: '', label: '', welcomeText: '', kbText: '', voice: '', minuteLimit: '' })

  async function load() {
    const { data } = await api.get<Demo[]>('/api/admin/demos')
    setDemos(data)
  }
  useEffect(() => {
    Promise.all([
      load(),
      api.get<{ voices: Voice[] }>('/api/voices').then(({ data }) => setVoices(data.voices)),
    ]).finally(() => setLoading(false))
  }, [])

  async function create() {
    setMsg('')
    if (!form.label.trim()) return setMsg('نام دمو الزامی است.')
    const extension = Number(form.extension)
    if (!Number.isInteger(extension) || extension < 1 || extension > 999)
      return setMsg('شماره داخلی باید یک عدد صحیح بین ۱ تا ۹۹۹ باشد.')
    if (extension >= 100 && extension <= 300)
      return setMsg('بازهٔ داخلی ۱۰۰ تا ۳۰۰ برای تلفن‌های انسانی رزرو است.')
    setCreating(true)
    try {
      await api.post('/api/admin/demos', {
        extension,
        label: form.label,
        welcomeText: form.welcomeText,
        kbText: form.kbText,
        voice: form.voice || null,
        minuteLimit: form.minuteLimit === '' ? null : Number(form.minuteLimit),
      })
      setForm({ extension: '', label: '', welcomeText: '', kbText: '', voice: '', minuteLimit: '' })
      setMsg('دمو ساخته شد.')
      await load()
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setCreating(false)
    }
  }

  return (
    <div className="space-y-6">
      <TutorialVideoCard />

      <Card className="animate-in">
        <h3 className="text-lg font-bold text-slate-800">ساخت دمو جدید</h3>
        <p className="mt-1 text-sm text-slate-500">
          داخلی دمو را خودتان از بازهٔ ۱ تا ۹۹۹ انتخاب کنید. بازهٔ ۱۰۰ تا ۳۰۰ برای تلفن‌های انسانی رزرو است.
        </p>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          <TextInput label="نام دمو" value={form.label} onChange={(e) => setForm({ ...form, label: e.target.value })} />
          <TextInput
            label="شماره داخلی"
            hint="مثلاً ۲ یا ۹۰۰؛ بازهٔ ۱۰۰ تا ۳۰۰ مجاز نیست."
            type="number"
            min={1}
            max={999}
            step={1}
            required
            value={form.extension}
            onChange={(e) => setForm({ ...form, extension: e.target.value })}
          />
          <TextInput
            label="محدودیت مکالمه (دقیقه)"
            type="number"
            value={form.minuteLimit}
            onChange={(e) => setForm({ ...form, minuteLimit: e.target.value })}
          />
          <div className="sm:col-span-2">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">گوینده</span>
            <VoiceSelect value={form.voice} onChange={(v) => setForm({ ...form, voice: v })} voices={voices} />
          </div>
          <div className="sm:col-span-2">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">پیام خوش‌آمد</span>
            <textarea
              rows={2}
              value={form.welcomeText}
              onChange={(e) => setForm({ ...form, welcomeText: e.target.value })}
              className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400"
            />
          </div>
          <div className="sm:col-span-2 rounded-xl border border-brand-100 bg-brand-50/40 px-4 py-3 text-sm leading-7 text-brand-800">پس از ساخت دمو، سؤال‌وجواب‌های نامحدود و پیام سؤال بی‌پاسخ را از بخش «پایگاه دانش سؤال و جواب» همان دمو اضافه کنید.</div>
        </div>
        <div className="mt-4 flex items-center gap-4">
          <Button onClick={create} loading={creating}>
            ساخت دمو
          </Button>
          {msg && <span className={cn('text-sm', msg.includes('ساخته') ? 'text-emerald-600' : 'text-rose-600')}>{msg}</span>}
        </div>
      </Card>

      <div className="space-y-3">
        <h3 className="text-lg font-bold text-slate-800">دموهای موجود ({toFa(demos.length)})</h3>
        {loading &&
          Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="flex items-center gap-3 rounded-2xl border border-slate-200/60 bg-white/85 p-4 shadow-soft">
              <Skeleton className="h-10 w-10 rounded-xl" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-3.5 w-40" />
                <Skeleton className="h-3 w-24" />
              </div>
            </div>
          ))}
        {!loading && demos.length === 0 && <p className="text-sm text-slate-400">هنوز دمویی ساخته نشده است.</p>}
        {demos.map((d) => (
          <DemoRow key={d.id} demo={d} voices={voices} onChanged={load} />
        ))}
      </div>
    </div>
  )
}
