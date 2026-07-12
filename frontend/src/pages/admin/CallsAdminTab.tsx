import { useEffect, useRef, useState } from 'react'
import { Search, Play, Pause, Trash2, ChevronDown, MessageSquare, Phone } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { Button, Card, cn } from '../../components/ui'
import { faDateTime, faDuration, toFa } from '../../lib/format'

interface CallRow {
  id: number
  callerId?: string | null
  startedAt: string
  durationSeconds: number
  answeredFromKb: boolean
  extension?: number | null
  ownerPhone: string
  ownerName: string
  brand?: string | null
  isDemo: boolean
  demoLabel?: string | null
  hasRecording: boolean
}
interface Turn {
  role: string
  text: string
}

function RecordingPlayer({ id }: { id: number }) {
  const [url, setUrl] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [playing, setPlaying] = useState(false)
  const audioRef = useRef<HTMLAudioElement | null>(null)

  useEffect(() => () => { if (url) URL.revokeObjectURL(url) }, [url])

  async function toggle() {
    if (playing) {
      audioRef.current?.pause()
      return
    }
    if (!url) {
      setLoading(true)
      try {
        const { data } = await api.get(`/api/admin/calls/${id}/recording`, { responseType: 'blob' })
        const objUrl = URL.createObjectURL(data as Blob)
        setUrl(objUrl)
        const audio = new Audio(objUrl)
        audioRef.current = audio
        audio.onended = () => setPlaying(false)
        audio.onpause = () => setPlaying(false)
        audio.onplay = () => setPlaying(true)
        await audio.play()
      } catch {
        /* ignore */
      } finally {
        setLoading(false)
      }
    } else {
      await audioRef.current?.play()
    }
  }

  return (
    <button
      onClick={toggle}
      className="flex items-center gap-1.5 rounded-lg bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 transition-colors hover:bg-brand-100"
    >
      {loading ? (
        <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-brand-300 border-t-brand-600" />
      ) : playing ? (
        <Pause size={14} />
      ) : (
        <Play size={14} />
      )}
      {playing ? 'توقف' : 'پخش'}
    </button>
  )
}

function CallItem({ call, onDeleted }: { call: CallRow; onDeleted: () => void }) {
  const [open, setOpen] = useState(false)
  const [turns, setTurns] = useState<Turn[] | null>(null)
  const [busy, setBusy] = useState(false)

  async function loadTranscript() {
    if (turns) return
    try {
      const { data } = await api.get(`/api/admin/calls/${call.id}`)
      const parsed = data.transcript ? JSON.parse(data.transcript) : []
      setTurns(Array.isArray(parsed) ? parsed : [])
    } catch {
      setTurns([])
    }
  }

  async function remove() {
    if (!confirm('این مکالمه و فایل صوتی آن حذف شود؟')) return
    setBusy(true)
    try {
      await api.delete(`/api/admin/calls/${call.id}`)
      onDeleted()
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rounded-2xl border border-slate-200">
      <div className="flex flex-wrap items-center justify-between gap-3 p-4">
        <div className="flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-xl bg-slate-50 text-slate-500">
            <Phone size={18} />
          </span>
          <div className="text-sm">
            <div className="font-semibold text-slate-800">
              {call.isDemo ? `دمو: ${call.demoLabel ?? ''}` : call.brand || call.ownerName || 'بدون نام'}
              <span className="mr-2 text-xs font-normal text-slate-400">داخلی {call.extension != null ? toFa(call.extension) : '—'}</span>
            </div>
            <div className="mt-0.5 text-xs text-slate-400">
              {faDateTime(call.startedAt)} · مدت {faDuration(call.durationSeconds)} ·{' '}
              <span dir="ltr">{toFa(call.callerId || call.ownerPhone)}</span>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <span
            className={cn(
              'rounded-full px-2.5 py-1 text-xs',
              call.answeredFromKb ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700',
            )}
          >
            {call.answeredFromKb ? 'از پایگاه دانش' : 'خارج از پایگاه دانش'}
          </span>
          {call.hasRecording && <RecordingPlayer id={call.id} />}
          <button
            onClick={() => {
              setOpen((o) => !o)
              loadTranscript()
            }}
            className="flex items-center gap-1 rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-200"
          >
            <MessageSquare size={13} /> متن
            <ChevronDown size={13} className={cn('transition-transform', open && 'rotate-180')} />
          </button>
          <button
            onClick={remove}
            disabled={busy}
            className="grid h-8 w-8 place-items-center rounded-lg bg-rose-50 text-rose-600 hover:bg-rose-100 disabled:opacity-50"
            title="حذف"
          >
            <Trash2 size={15} />
          </button>
        </div>
      </div>

      {open && (
        <div className="border-t border-slate-100 p-4">
          {turns === null ? (
            <p className="text-sm text-slate-400">در حال بارگذاری متن…</p>
          ) : turns.length === 0 ? (
            <p className="text-sm text-slate-400">متنی برای این مکالمه ثبت نشده است.</p>
          ) : (
            <div className="space-y-2">
              {turns.map((t, i) => (
                <div
                  key={i}
                  className={cn(
                    'max-w-[85%] rounded-2xl px-3.5 py-2 text-sm',
                    t.role === 'assistant'
                      ? 'bg-brand-50 text-slate-800'
                      : 'mr-auto bg-slate-100 text-slate-700',
                  )}
                >
                  <div className="mb-0.5 text-[10px] text-slate-400">{t.role === 'assistant' ? 'هوش مصنوعی' : 'تماس‌گیرنده'}</div>
                  {t.text}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

export default function CallsAdminTab() {
  const [items, setItems] = useState<CallRow[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [q, setQ] = useState('')

  async function load() {
    setLoading(true)
    try {
      const { data } = await api.get('/api/admin/calls', { params: { from, to, q, pageSize: 100 } })
      setItems(data.items)
      setTotal(data.total)
    } catch (e) {
      console.error(apiError(e))
    } finally {
      setLoading(false)
    }
  }
  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="space-y-5">
      <Card className="animate-in">
        <h3 className="text-lg font-bold text-slate-800">مکالمه‌ها</h3>
        <p className="mt-1 text-sm text-slate-500">
          مشاهده، پخش، حذف و جست‌وجوی مکالمه‌های پاسخ‌داده‌شده توسط هوش مصنوعی. تاریخ‌ها شمسی است.
        </p>
        <div className="mt-4 grid gap-3 sm:grid-cols-4">
          <div>
            <label className="mb-1.5 block text-xs text-slate-500">از تاریخ</label>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-brand-400" />
          </div>
          <div>
            <label className="mb-1.5 block text-xs text-slate-500">تا تاریخ</label>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-brand-400" />
          </div>
          <div className="sm:col-span-2">
            <label className="mb-1.5 block text-xs text-slate-500">جست‌وجو (شماره موبایل / برند)</label>
            <input value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && load()} placeholder="مثلاً 0912…" dir="rtl" className="h-11 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-brand-400" />
          </div>
        </div>
        <div className="mt-4 flex items-center gap-3">
          <Button onClick={load} loading={loading}>
            <Search size={16} /> اعمال فیلتر
          </Button>
          <button
            onClick={() => {
              setFrom('')
              setTo('')
              setQ('')
              setTimeout(load, 0)
            }}
            className="text-sm text-slate-500 hover:text-brand-600"
          >
            پاک‌کردن فیلترها
          </button>
          <span className="text-sm text-slate-400">{toFa(total)} مکالمه</span>
        </div>
      </Card>

      <div className="space-y-2">
        {loading && <p className="text-sm text-slate-400">در حال بارگذاری…</p>}
        {!loading && items.length === 0 && (
          <Card className="animate-in py-10 text-center">
            <p className="text-sm text-slate-500">مکالمه‌ای با این فیلتر یافت نشد.</p>
          </Card>
        )}
        {items.map((c) => (
          <CallItem key={c.id} call={c} onDeleted={load} />
        ))}
      </div>
    </div>
  )
}
