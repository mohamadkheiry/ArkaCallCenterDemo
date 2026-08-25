import { useEffect, useState } from 'react'
import { CheckCircle2, Database, Pencil, Plus, RefreshCw, Save, Trash2, Volume2 } from 'lucide-react'
import AudioPlayButton from '../components/AudioPlayButton'
import { Button, Card, SkeletonCard, cn } from '../components/ui'
import { api, apiError } from '../lib/api'
import { toFa } from '../lib/format'

interface AnswerItem {
  id: number
  question: string
  answer: string
  sortOrder: number
  audioStatus: 'Pending' | 'Ready' | 'Failed'
  audioError?: string | null
  updatedAt: string
}

interface AnswerPage { total: number; items: AnswerItem[] }
interface FallbackInfo { text?: string | null; audioReady: boolean; updatedAt?: string | null }
interface KnowledgeInfo { legacyContentPreserved?: boolean }
const PAGE_SIZE = 100

function AnswerRow({ item, onChanged }: { item: AnswerItem; onChanged: () => Promise<void> }) {
  const [editing, setEditing] = useState(false)
  const [question, setQuestion] = useState(item.question)
  const [answer, setAnswer] = useState(item.answer)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function save() {
    if (!question.trim() || !answer.trim()) return setError('سؤال و پاسخ هر دو الزامی هستند.')
    setBusy(true)
    setError('')
    try {
      await api.put(`/api/knowledge-base/answers/${item.id}`, { question, answer })
      setEditing(false)
      await onChanged()
    } catch (e) { setError(apiError(e)) } finally { setBusy(false) }
  }

  async function remove() {
    if (!confirm('این سؤال و پاسخ حذف شود؟')) return
    setBusy(true)
    try {
      await api.delete(`/api/knowledge-base/answers/${item.id}`)
      await onChanged()
    } catch (e) { setError(apiError(e)) } finally { setBusy(false) }
  }

  async function regenerate() {
    setBusy(true)
    setError('')
    try {
      await api.post(`/api/knowledge-base/answers/${item.id}/regenerate-audio`)
      await onChanged()
    } catch (e) { setError(apiError(e)) } finally { setBusy(false) }
  }

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 transition-shadow hover:shadow-soft">
      {editing ? (
        <div className="space-y-3">
          <label className="block">
            <span className="mb-1.5 block text-xs font-bold text-slate-600">سؤال</span>
            <textarea rows={2} maxLength={500} value={question} onChange={(e) => setQuestion(e.target.value)}
              className="w-full resize-y rounded-xl border border-slate-200 p-3 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100" />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-xs font-bold text-slate-600">پاسخ قابل پخش</span>
            <textarea rows={4} maxLength={4000} value={answer} onChange={(e) => setAnswer(e.target.value)}
              className="w-full resize-y rounded-xl border border-slate-200 p-3 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100" />
          </label>
          <div className="flex flex-wrap gap-2">
            <Button className="h-10 px-4" onClick={save} loading={busy}><Save size={16} /> ذخیره و بازتولید صوت</Button>
            <Button className="h-10 px-4" variant="ghost" onClick={() => {
              setQuestion(item.question); setAnswer(item.answer); setEditing(false); setError('')
            }}>انصراف</Button>
          </div>
        </div>
      ) : (
        <>
          <div className="flex items-start gap-3">
            <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-brand-50 text-sm font-extrabold text-brand-700">{toFa(item.sortOrder + 1)}</span>
            <div className="min-w-0 flex-1">
              <h3 className="font-bold leading-7 text-slate-800">{item.question}</h3>
              <p className="mt-1 whitespace-pre-wrap text-sm leading-7 text-slate-600">{item.answer}</p>
            </div>
          </div>
          <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-slate-100 pt-3">
            {item.audioStatus === 'Ready' ? (
              <>
                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-700"><CheckCircle2 size={13} /> صوت آماده</span>
                <AudioPlayButton path={`/api/knowledge-base/answers/${item.id}/audio?v=${encodeURIComponent(item.updatedAt)}`} />
              </>
            ) : <span className="rounded-full bg-rose-50 px-2.5 py-1 text-xs text-rose-700">{item.audioError || 'صوت آماده نیست'}</span>}
            <button type="button" onClick={() => setEditing(true)} className="mr-auto inline-flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100"><Pencil size={14} /> ویرایش</button>
            <button type="button" disabled={busy} onClick={regenerate} className="inline-flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 disabled:opacity-50"><RefreshCw size={14} /> بازتولید صوت</button>
            <button type="button" disabled={busy} onClick={remove} className="inline-flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium text-rose-600 hover:bg-rose-50 disabled:opacity-50"><Trash2 size={14} /> حذف</button>
          </div>
        </>
      )}
      {error && <p className="mt-3 rounded-xl bg-rose-50 px-3 py-2 text-xs text-rose-700">{error}</p>}
    </div>
  )
}

export default function KnowledgeBasePage() {
  const [items, setItems] = useState<AnswerItem[]>([])
  const [total, setTotal] = useState(0)
  const [fallback, setFallback] = useState('')
  const [fallbackReady, setFallbackReady] = useState(false)
  const [fallbackUpdatedAt, setFallbackUpdatedAt] = useState<string | null>(null)
  const [question, setQuestion] = useState('')
  const [answer, setAnswer] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState<'answer' | 'fallback' | ''>('')
  const [message, setMessage] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)
  const [legacyPreserved, setLegacyPreserved] = useState(false)

  async function load(reset = true) {
    const skip = reset ? 0 : items.length
    const [answersResponse, fallbackResponse, infoResponse] = await Promise.all([
      api.get<AnswerPage>('/api/knowledge-base/answers', { params: { skip, take: PAGE_SIZE } }),
      api.get<FallbackInfo>('/api/knowledge-base/fallback'),
      api.get<KnowledgeInfo | null>('/api/knowledge-base'),
    ])
    setItems((current) => reset ? answersResponse.data.items : [...current, ...answersResponse.data.items])
    setTotal(answersResponse.data.total)
    setFallback(fallbackResponse.data.text ?? '')
    setFallbackReady(fallbackResponse.data.audioReady)
    setFallbackUpdatedAt(fallbackResponse.data.updatedAt ?? null)
    setLegacyPreserved(!!infoResponse.data?.legacyContentPreserved)
  }

  useEffect(() => { load().finally(() => setLoading(false)) }, []) // eslint-disable-line react-hooks/exhaustive-deps

  async function addAnswer() {
    setMessage(null)
    if (!question.trim() || !answer.trim()) return setMessage({ type: 'err', text: 'برای افزودن، سؤال و پاسخ را کامل کنید.' })
    setBusy('answer')
    try {
      await api.post('/api/knowledge-base/answers', { question, answer })
      setQuestion(''); setAnswer('')
      setMessage({ type: 'ok', text: 'سؤال ذخیره شد و فایل صوتی پاسخ آماده است.' })
      await load(true)
    } catch (e) { setMessage({ type: 'err', text: apiError(e) }) } finally { setBusy('') }
  }

  async function saveFallback() {
    setMessage(null)
    if (!fallback.trim()) return setMessage({ type: 'err', text: 'پیام سؤال بی‌پاسخ نمی‌تواند خالی باشد.' })
    setBusy('fallback')
    try {
      const { data } = await api.put<FallbackInfo>('/api/knowledge-base/fallback', { text: fallback })
      setFallbackReady(data.audioReady)
      setFallbackUpdatedAt(data.updatedAt ?? new Date().toISOString())
      setMessage({ type: 'ok', text: 'پیام جایگزین ذخیره و به صوت ثابت تبدیل شد.' })
    } catch (e) { setMessage({ type: 'err', text: apiError(e) }) } finally { setBusy('') }
  }

  if (loading) return <div className="mx-auto max-w-5xl"><SkeletonCard lines={8} /></div>

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-extrabold text-slate-800"><Database className="text-brand-600" /> پایگاه دانش سؤال و جواب</h1>
          <p className="mt-1 max-w-3xl text-sm leading-7 text-slate-500">هر تعداد سؤال که نیاز دارید اضافه کنید. پاسخ هر سؤال فقط یک‌بار به صوت تبدیل می‌شود و تماس‌ها همان فایل تأییدشده را پخش می‌کنند.</p>
        </div>
        <span className="rounded-full bg-brand-50 px-3 py-1.5 text-xs font-bold text-brand-700">{toFa(total)} سؤال فعال</span>
      </div>

      {legacyPreserved && <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm leading-7 text-amber-800">محتوای متنی یا فایل قبلی شما برای حفظ داده‌ها پاک نشده است؛ مسیر جدید تماس فقط از سؤال‌وجواب‌های زیر استفاده می‌کند.</div>}

      <Card className="overflow-hidden border-brand-100 bg-gradient-to-br from-white to-brand-50/40">
        <div className="flex items-center gap-3">
          <span className="grid h-11 w-11 place-items-center rounded-2xl bg-brand-100 text-brand-700"><Plus size={20} /></span>
          <div><h2 className="font-extrabold text-slate-800">افزودن سؤال و پاسخ</h2><p className="text-xs leading-6 text-slate-500">صورت‌های رایج سؤال را بنویسید؛ جست‌وجو اختلاف نگارشی و شباهت جمله را هم در نظر می‌گیرد.</p></div>
        </div>
        <div className="mt-5 grid gap-4 lg:grid-cols-2">
          <label><span className="mb-1.5 block text-sm font-bold text-slate-700">سؤال تماس‌گیرنده</span>
            <textarea rows={5} maxLength={500} value={question} onChange={(e) => setQuestion(e.target.value)} placeholder="مثلاً کلاس‌های عصر چه ساعتی برگزار می‌شوند؟" className="w-full resize-y rounded-xl border border-slate-200 bg-white p-4 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100" /></label>
          <label><span className="mb-1.5 block text-sm font-bold text-slate-700">پاسخی که باید پخش شود</span>
            <textarea rows={5} maxLength={4000} value={answer} onChange={(e) => setAnswer(e.target.value)} placeholder="متن نهایی و دقیق پاسخ را وارد کنید." className="w-full resize-y rounded-xl border border-slate-200 bg-white p-4 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100" /></label>
        </div>
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
          <span className="inline-flex items-center gap-1.5 text-xs text-slate-500"><Volume2 size={15} /> صوت پاسخ هنگام ذخیره ساخته می‌شود.</span>
          <Button onClick={addAnswer} loading={busy === 'answer'}><Plus size={17} /> افزودن و تولید صوت</Button>
        </div>
      </Card>

      <Card>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div><h2 className="font-extrabold text-slate-800">پیام سؤال بی‌پاسخ</h2><p className="mt-1 text-xs leading-6 text-slate-500">فقط زمانی پخش می‌شود که هیچ سؤال مشابه و قابل اتکایی پیدا نشود.</p></div>
          <span className={cn('rounded-full px-2.5 py-1 text-xs', fallbackReady ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700')}>{fallbackReady ? 'صوت آماده است' : 'نیازمند تولید صوت'}</span>
        </div>
        <textarea rows={3} maxLength={1500} value={fallback} onChange={(e) => setFallback(e.target.value)} className="mt-4 w-full resize-y rounded-xl border border-slate-200 p-4 text-sm leading-7 outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100" />
        <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
          {fallbackReady && <AudioPlayButton path={`/api/knowledge-base/fallback/audio?v=${encodeURIComponent(fallbackUpdatedAt ?? 'current')}`} />}
          <Button variant="outline" onClick={saveFallback} loading={busy === 'fallback'}><Save size={16} /> ذخیره و تولید صوت</Button>
        </div>
      </Card>

      {message && <div className={cn('rounded-2xl px-4 py-3 text-sm', message.type === 'ok' ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700')}>{message.text}</div>}

      <section className="space-y-3">
        <h2 className="text-lg font-extrabold text-slate-800">سؤال‌وجواب‌های ذخیره‌شده</h2>
        {items.length === 0 ? <Card className="py-12 text-center text-sm text-slate-500">هنوز سؤال و پاسخی اضافه نشده است.</Card> : items.map((item) => <AnswerRow key={item.id} item={item} onChanged={() => load(true)} />)}
        {items.length < total && <div className="flex justify-center"><Button variant="outline" onClick={() => load(false)}>نمایش موارد بیشتر</Button></div>}
      </section>
    </div>
  )
}
