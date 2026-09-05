import { useEffect, useRef, useState } from 'react'
import {
  CloudUpload,
  FileText,
  FileType2,
  Info,
  Save,
  Trash2,
} from 'lucide-react'
import { api, apiError } from '../lib/api'
import { toFa } from '../lib/format'
import { Button, Card, SkeletonCard, cn } from '../components/ui'

const MAX_CHARS = 2000
const MAX_FILE = 100 * 1024

interface KbInfo {
  sourceType: 'Text' | 'File'
  rawText?: string | null
  fileName?: string | null
  charCount: number
  fileSizeBytes: number
  moderationStatus: string
  updatedAt: string
}

export default function KnowledgeBasePage() {
  const [kb, setKb] = useState<KbInfo | null>(null)
  const [mode, setMode] = useState<'text' | 'file'>('text')
  const [text, setText] = useState('')
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(
    null,
  )
  const fileRef = useRef<HTMLInputElement>(null)

  async function load() {
    setLoading(true)
    try {
      const { data } = await api.get<KbInfo | null>('/api/knowledge-base')
      setKb(data)
      if (data?.sourceType === 'Text' && data.rawText) setText(data.rawText)
    } catch (e) {
      setMsg({
        type: 'err',
        text: apiError(e, 'دریافت پایگاه دانش ممکن نشد؛ دوباره تلاش کنید.'),
      })
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  async function saveText() {
    setMsg(null)
    if (!text.trim())
      return setMsg({ type: 'err', text: 'متن نمی‌تواند خالی باشد.' })
    if (text.length > MAX_CHARS)
      return setMsg({
        type: 'err',
        text: `حداکثر ${toFa(MAX_CHARS)} کاراکتر مجاز است.`,
      })
    setBusy(true)
    try {
      await api.post('/api/knowledge-base/text', { text })
      setMsg({ type: 'ok', text: 'پایگاه دانش ذخیره و بررسی شد.' })
      await load()
    } catch (e) {
      setMsg({ type: 'err', text: apiError(e) })
    } finally {
      setBusy(false)
    }
  }

  async function uploadFile(file: File) {
    setMsg(null)
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'))
    if (!['.txt', '.docx'].includes(ext))
      return setMsg({
        type: 'err',
        text: 'فقط فایل txt و Word (docx) مجاز است.',
      })
    if (file.size > MAX_FILE)
      return setMsg({
        type: 'err',
        text: 'حجم فایل باید حداکثر ۱۰۰ کیلوبایت باشد.',
      })
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', file)
      await api.post('/api/knowledge-base/file', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      setMsg({ type: 'ok', text: 'فایل پردازش و ذخیره شد.' })
      await load()
    } catch (e) {
      setMsg({ type: 'err', text: apiError(e) })
    } finally {
      setBusy(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  async function remove() {
    if (!confirm('پایگاه دانش حذف شود؟')) return
    setBusy(true)
    try {
      await api.delete('/api/knowledge-base')
      setText('')
      setMsg({ type: 'ok', text: 'پایگاه دانش حذف شد.' })
      await load()
    } catch (e) {
      setMsg({ type: 'err', text: apiError(e) })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">پایگاه دانش</h1>
        <p className="mt-1 text-sm text-slate-500">
          محتوایی که هوش مصنوعی برای پاسخ به تماس‌ها از آن استفاده می‌کند.
          حداکثر یک متن {toFa(MAX_CHARS)} کاراکتری یا یک فایل {toFa(100)}{' '}
          کیلوبایتی (txt یا Word).
        </p>
      </div>

      {loading && !kb && <SkeletonCard lines={2} />}

      <Card className="animate-in">
        {kb && (
          <div className="mb-6 border-b border-slate-100 pb-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                <span className="grid h-10 w-10 place-items-center rounded-xl bg-emerald-50 text-emerald-600">
                  {kb.sourceType === 'File' ? (
                    <FileType2 size={19} />
                  ) : (
                    <FileText size={19} />
                  )}
                </span>
                <div>
                  <div className="text-sm font-semibold text-slate-800">
                    {kb.sourceType === 'File'
                      ? kb.fileName
                      : 'پایگاه دانش متنی'}
                  </div>
                  <div className="text-xs text-slate-400">
                    {toFa(kb.charCount)} کاراکتر · وضعیت بررسی:{' '}
                    {kb.moderationStatus === 'Approved'
                      ? 'تأییدشده'
                      : kb.moderationStatus}
                  </div>
                </div>
              </div>
              <Button variant="danger" onClick={remove} loading={busy}>
                <Trash2 size={16} />
                حذف
              </Button>
            </div>
          </div>
        )}

        <div
          className="mb-5 flex rounded-xl border border-slate-200 bg-slate-50 p-1"
          aria-label="روش ورود دانش"
        >
          {(['text', 'file'] as const).map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              aria-pressed={mode === m}
              className={cn(
                'flex-1 rounded-lg px-4 py-2.5 text-sm font-medium transition-colors',
                mode === m
                  ? 'bg-white text-brand-700 shadow-sm'
                  : 'text-slate-500',
              )}
            >
              {m === 'text' ? 'ورود متن' : 'بارگذاری فایل'}
            </button>
          ))}
        </div>

        {mode === 'text' ? (
          <div className="space-y-3">
            <label
              htmlFor="knowledge-content"
              className="block text-sm font-semibold text-slate-700"
            >
              اطلاعات کسب‌وکار
            </label>
            <textarea
              id="knowledge-content"
              value={text}
              onChange={(e) => setText(e.target.value)}
              rows={7}
              maxLength={MAX_CHARS}
              placeholder="اطلاعات کسب‌وکار، خدمات، ساعات کاری، سوالات پرتکرار و ..."
              className="w-full resize-none rounded-xl border border-slate-200 bg-white p-4 text-sm leading-7 text-slate-800 outline-none transition-all focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
            />
            <div className="flex items-center justify-between">
              <span
                className={cn(
                  'text-xs',
                  text.length > MAX_CHARS ? 'text-rose-600' : 'text-slate-400',
                )}
              >
                {toFa(text.length)} / {toFa(MAX_CHARS)}
              </span>
              <Button onClick={saveText} loading={busy}>
                <Save size={17} />
                ذخیره و بررسی
              </Button>
            </div>
          </div>
        ) : (
          <label
            className="flex cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/50 p-10 text-center transition-colors hover:border-brand-300 hover:bg-brand-50/40"
            onDrop={(e) => {
              e.preventDefault()
              if (e.dataTransfer.files[0]) uploadFile(e.dataTransfer.files[0])
            }}
            onDragOver={(e) => e.preventDefault()}
          >
            <CloudUpload size={34} className="text-brand-500" />
            <span className="text-sm font-medium text-slate-700">
              فایل را اینجا رها کنید یا کلیک کنید
            </span>
            <span className="text-xs text-slate-400">
              txt یا Word (docx) · حداکثر ۱۰۰ کیلوبایت
            </span>
            <input
              ref={fileRef}
              type="file"
              accept=".txt,.docx"
              className="hidden"
              onChange={(e) =>
                e.target.files?.[0] && uploadFile(e.target.files[0])
              }
            />
          </label>
        )}

        {msg && (
          <div
            className={cn(
              'mt-4 rounded-xl px-4 py-3 text-sm',
              msg.type === 'ok'
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-rose-50 text-rose-700',
            )}
          >
            {msg.text}
          </div>
        )}
        {loading && (
          <p className="mt-4 text-sm text-slate-400">در حال بارگذاری…</p>
        )}
      </Card>
      <div className="knowledge-note">
        <Info size={20} />
        ساعات کاری، خدمات و راه‌های ارتباطی را دقیق بنویسید. پس از ویرایش،
        تغییرات را ذخیره کنید تا در پاسخ‌گویی استفاده شوند.
      </div>
    </div>
  )
}
