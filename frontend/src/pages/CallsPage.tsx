import { useEffect, useState } from 'react'
import {
  Inbox,
  MessageCircleQuestion,
  ChevronDown,
  Search,
  RefreshCw,
} from 'lucide-react'
import { api } from '../lib/api'
import { Button, Card, Skeleton, cn } from '../components/ui'
import AudioPlayButton from '../components/AudioPlayButton'
import { faDateTime, faDuration, toFa, toEn } from '../lib/format'

interface CallRow {
  id: number
  callerId?: string | null
  startedAt: string
  durationSeconds: number
  answeredFromKb: boolean
  hasRecording: boolean
}

interface UnansweredItem {
  callId: number
  index: number
  question: string
  callerId?: string | null
  startedAt: string
}

/** بخشِ «سوالاتِ بی‌پاسخ»: با کلیک باز می‌شود و لیست را می‌گیرد؛ هر سوال به‌صورت صوتی قابل پخش است. */
function UnansweredSection() {
  const [open, setOpen] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<UnansweredItem[]>([])
  const [error, setError] = useState('')

  async function loadItems() {
    setLoading(true)
    setError('')
    try {
      const { data } = await api.get<UnansweredItem[]>('/api/calls/unanswered')
      setItems(data)
      setLoaded(true)
    } catch {
      setError('دریافت سوالات بی‌پاسخ ممکن نشد. دوباره تلاش کنید.')
    } finally {
      setLoading(false)
    }
  }

  async function toggleOpen() {
    const next = !open
    setOpen(next)
    if (next && !loaded) await loadItems()
  }

  return (
    <Card className="animate-in">
      <button
        onClick={toggleOpen}
        aria-expanded={open}
        className="flex w-full items-center gap-3 text-right"
      >
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-amber-50 text-amber-600">
          <MessageCircleQuestion size={20} />
        </span>
        <span className="flex-1">
          <span className="block font-bold text-slate-800">سوالات بی‌پاسخ</span>
          <span className="block text-xs text-slate-500">
            سوالاتی که پاسخشان در پایگاه دانش نبود. برای شنیدنِ هر سوال روی
            «پخش» بزنید و در صورت نیاز، پایگاه دانش را کامل‌تر کنید.
          </span>
        </span>
        <ChevronDown
          size={18}
          className={cn(
            'shrink-0 text-slate-400 transition-transform',
            open && 'rotate-180',
          )}
        />
      </button>

      {open && (
        <div className="mt-4 border-t border-slate-100 pt-4">
          {loading ? (
            <p className="text-sm text-slate-400">در حال بارگذاری…</p>
          ) : error ? (
            <div className="error-notice" role="alert">
              {error}
              <Button variant="ghost" onClick={loadItems}>
                تلاش دوباره
              </Button>
            </div>
          ) : items.length === 0 ? (
            <p className="py-4 text-center text-sm text-slate-500">
              سوال بی‌پاسخی ثبت نشده است.
            </p>
          ) : (
            <ul className="space-y-2">
              {items.map((q) => (
                <li
                  key={`${q.callId}-${q.index}`}
                  className="flex items-center gap-3 rounded-xl border border-slate-200 p-3"
                >
                  <div className="min-w-0 flex-1">
                    <p
                      className="text-sm leading-7 text-slate-700"
                      title={q.question}
                    >
                      {q.question}
                    </p>
                    <p className="mt-0.5 text-xs text-slate-400">
                      <span dir="ltr">
                        {q.callerId ? toFa(q.callerId) : 'نامشخص'}
                      </span>{' '}
                      · {faDateTime(q.startedAt)}
                    </p>
                  </div>
                  <AudioPlayButton
                    path={`/api/calls/${q.callId}/unanswered/${q.index}/audio`}
                  />
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Card>
  )
}

export default function CallsPage() {
  const [calls, setCalls] = useState<CallRow[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const [error, setError] = useState('')

  async function loadCalls() {
    setLoading(true)
    setError('')
    try {
      const { data } = await api.get<CallRow[]>('/api/calls')
      setCalls(data)
    } catch {
      setError('دریافت تماس‌ها ممکن نشد. اتصال را بررسی و دوباره تلاش کنید.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadCalls()
  }, [])

  const filtered = calls.filter(
    (c) =>
      (!search || toEn(c.callerId ?? '').includes(toEn(search.trim()))) &&
      (filter === 'all' ||
        (filter === 'kb' ? c.answeredFromKb : !c.answeredFromKb)),
  )

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">تماس‌ها</h1>
        <p className="mt-1 text-sm text-slate-500">
          تاریخچه‌ی تماس‌های پاسخ‌داده‌شده توسط هوش مصنوعی. می‌توانید مکالمه‌ی
          هر تماس را گوش دهید.
        </p>
      </div>

      <UnansweredSection />

      <Card className="animate-in">
        <div className="calls-toolbar">
          <label className="calls-search">
            <Search size={17} />
            <input
              className="ui-input"
              aria-label="جست‌وجوی تماس‌گیرنده"
              placeholder="جست‌وجوی شماره تماس‌گیرنده…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <select
            className="ui-input !w-auto"
            aria-label="فیلتر نوع پاسخ"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          >
            <option value="all">همه پاسخ‌ها</option>
            <option value="kb">از پایگاه دانش</option>
            <option value="other">خارج از پایگاه دانش</option>
          </select>
          <Button
            variant="outline"
            onClick={loadCalls}
            loading={loading}
            aria-label="به‌روزرسانی تماس‌ها"
          >
            <RefreshCw size={17} />
          </Button>
        </div>
        {error && (
          <div className="error-notice" role="alert">
            {error}
          </div>
        )}
        {loading ? (
          <div className="space-y-3">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="flex items-center gap-4">
                <Skeleton className="h-4 w-28" />
                <Skeleton className="h-4 w-24" />
                <Skeleton className="h-4 w-16" />
                <Skeleton className="ml-auto h-8 w-20 rounded-lg" />
              </div>
            ))}
          </div>
        ) : error ? null : filtered.length === 0 ? (
          <div className="py-10 text-center">
            <div className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-slate-50 text-slate-400">
              <Inbox size={26} />
            </div>
            <p className="text-sm text-slate-500">
              {calls.length === 0
                ? 'هنوز تماسی ثبت نشده است.'
                : 'تماسی با این جست‌وجو پیدا نشد.'}
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[620px] text-right text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-xs text-slate-400">
                  <th className="p-3 font-medium">تماس‌گیرنده</th>
                  <th className="p-3 font-medium">زمان</th>
                  <th className="p-3 font-medium">مدت</th>
                  <th className="p-3 font-medium">نوع پاسخ</th>
                  <th className="p-3 font-medium">مکالمه</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((c) => (
                  <tr key={c.id} className="border-b border-slate-100">
                    <td className="p-3 text-slate-700" dir="ltr">
                      {c.callerId ? toFa(c.callerId) : 'نامشخص'}
                    </td>
                    <td className="p-3 text-slate-500">
                      {faDateTime(c.startedAt)}
                    </td>
                    <td className="p-3 text-slate-600">
                      {faDuration(c.durationSeconds)}
                    </td>
                    <td className="p-3">
                      <span
                        className={cn(
                          'rounded-full px-2.5 py-1 text-xs',
                          c.answeredFromKb
                            ? 'bg-emerald-50 text-emerald-700'
                            : 'bg-amber-50 text-amber-700',
                        )}
                      >
                        {c.answeredFromKb
                          ? 'از پایگاه دانش'
                          : 'خارج از پایگاه دانش'}
                      </span>
                    </td>
                    <td className="p-3">
                      {c.hasRecording ? (
                        <AudioPlayButton
                          path={`/api/calls/${c.id}/recording`}
                        />
                      ) : (
                        <span className="text-xs text-slate-400">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  )
}
