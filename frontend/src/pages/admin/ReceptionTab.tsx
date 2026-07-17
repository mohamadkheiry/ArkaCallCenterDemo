import { useEffect, useRef, useState } from 'react'
import { Music, Upload } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { useFlash } from '../../lib/flash'
import { Button, Card, SkeletonCard } from '../../components/ui'

interface Voice {
  name: string
  displayName: string
}

export default function ReceptionTab() {
  const [text, setText] = useState('')
  const [voice, setVoice] = useState('alloy')
  const [voices, setVoices] = useState<Voice[]>([])
  const [sound, setSound] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const { flash: gFlash, ok: gOk, fail: gFail, clear: gClear } = useFlash()

  const greetingFileRef = useRef<HTMLInputElement>(null)

  const [holdEnabled, setHoldEnabled] = useState(false)
  const [holdHasFile, setHoldHasFile] = useState(false)
  const [loading, setLoading] = useState(true)
  const { flash: hFlash, ok: hOk, fail: hFail, clear: hClear } = useFlash()
  const holdRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    Promise.all([
      api.get('/api/admin/main-greeting').then(({ data }) => {
        setText(data.text ?? '')
        setVoice(data.voice ?? 'alloy')
        setSound(data.asteriskSound ?? null)
      }),
      api.get<{ voices: Voice[] }>('/api/voices').then(({ data }) => setVoices(data.voices)),
      api.get('/api/admin/hold-music').then(({ data }) => {
        setHoldEnabled(!!data.enabled)
        setHoldHasFile(!!data.hasFile)
      }),
    ]).finally(() => setLoading(false))
  }, [])

  if (loading) {
    return (
      <div className="space-y-6">
        <SkeletonCard lines={4} />
        <SkeletonCard lines={3} />
      </div>
    )
  }

  async function saveGreeting() {
    setBusy(true)
    gClear()
    try {
      const { data } = await api.put('/api/admin/main-greeting', { text, voice })
      gOk(data.message)
      setSound(data.asteriskSound ?? sound)
    } catch (e) {
      gFail(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  async function uploadGreetingFile(file: File) {
    gClear()
    if (!file.name.toLowerCase().endsWith('.wav')) return gFail('فقط فایل WAV (۱۶ بیت PCM) مجاز است.')
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/api/admin/main-greeting/file', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      gOk(data.message)
      setSound(data.asteriskSound ?? sound)
    } catch (e) {
      gFail(apiError(e))
    } finally {
      setBusy(false)
      if (greetingFileRef.current) greetingFileRef.current.value = ''
    }
  }

  async function uploadHold(file: File) {
    hClear()
    if (!file.name.toLowerCase().endsWith('.wav')) return hFail('فقط فایل WAV مجاز است.')
    try {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/api/admin/hold-music', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      hOk(data.message)
      setHoldHasFile(true)
      setHoldEnabled(true)
    } catch (e) {
      hFail(apiError(e))
    } finally {
      if (holdRef.current) holdRef.current.value = ''
    }
  }

  async function toggleHold(enabled: boolean) {
    // بدون فایلِ موسیقی نمی‌توان انتظار را فعال کرد.
    if (enabled && !holdHasFile) {
      hFail('ابتدا یک فایل موسیقی انتظار بارگذاری کنید.')
      return
    }
    hClear()
    const prev = holdEnabled
    setHoldEnabled(enabled) // خوش‌بینانه
    try {
      await api.put('/api/admin/hold-music/enabled', { enabled })
    } catch (e) {
      setHoldEnabled(prev) // در صورت خطا به حالت قبل برگرد
      hFail(apiError(e))
    }
  }

  return (
    <div className="space-y-6">
      <Card className="animate-in">
        <h3 className="text-lg font-bold text-slate-800">پیام پذیرش اصلی (IVR)</h3>
        <p className="mt-1 text-sm text-slate-500">
          هنگام تماس با شماره‌ی اصلی شرکت، این پیام پخش می‌شود و سپس منتظر دریافت شماره داخلی می‌ماند.
          یا متن را با هوش مصنوعی به صوت تبدیل کنید، یا یک فایل صوتی دلخواه (WAV) بارگذاری کنید.
        </p>

        <div className="mt-4 rounded-2xl border border-slate-200 bg-slate-50/60 p-4">
          <div className="flex items-center gap-2 text-sm font-medium text-slate-700">
            <Upload size={16} className="text-brand-600" />
            بارگذاری فایل صوتی دلخواه (به‌جای تولید با هوش مصنوعی)
          </div>
          <p className="mt-1 text-xs text-slate-400">فایل WAV (۱۶ بیت PCM) — به‌صورت خودکار به ۸kHz مونو تبدیل و روی ایزابل آپلود می‌شود.</p>
          <label className="mt-3 inline-flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:border-brand-300 hover:text-brand-700">
            <Upload size={15} />
            {busy ? 'در حال پردازش…' : 'انتخاب و آپلود فایل WAV'}
            <input
              ref={greetingFileRef}
              type="file"
              accept=".wav"
              className="hidden"
              disabled={busy}
              onChange={(e) => e.target.files?.[0] && uploadGreetingFile(e.target.files[0])}
            />
          </label>
        </div>

        <div className="mt-4 space-y-4">
          <span className="block text-sm font-medium text-slate-700">یا: تولید از روی متن (هوش مصنوعی)</span>
          <textarea
            rows={3}
            value={text}
            onChange={(e) => setText(e.target.value)}
            className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
          />
          <div>
            <span className="mb-1.5 block text-sm font-medium text-slate-700">گوینده</span>
            <select
              value={voice}
              onChange={(e) => setVoice(e.target.value)}
              className="h-12 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm outline-none focus:border-brand-400"
            >
              {voices.map((v) => (
                <option key={v.name} value={v.name}>
                  {v.displayName} ({v.name})
                </option>
              ))}
            </select>
          </div>
          <div className="text-sm">
            <span className={sound ? 'text-emerald-600' : 'text-slate-400'}>
              {sound ? `روی ایزابل آپلود شده: ${sound}` : 'هنوز روی ایزابل آپلود نشده'}
            </span>
          </div>
          <div className="flex items-center gap-4">
            <Button onClick={saveGreeting} loading={busy}>
              ذخیره، تولید صوت و آپلود
            </Button>
            {gFlash && <span className={`text-sm ${gFlash.ok ? 'text-emerald-600' : 'text-rose-600'}`}>{gFlash.text}</span>}
          </div>
        </div>
      </Card>

      <Card className="animate-in">
        <h3 className="text-lg font-bold text-slate-800">موسیقی انتظار (حین فکر کردن هوش مصنوعی)</h3>
        <p className="mt-1 text-sm text-slate-500">
          وقتی هوش مصنوعی در حال پردازش پاسخ است، این موسیقی برای تماس‌گیرنده پخش می‌شود. فایل WAV (۱۶ بیت) بارگذاری کنید.
        </p>
        <div className="mt-4 space-y-4">
          <label className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/50 p-8 text-center hover:border-brand-300">
            <Music size={30} className="text-brand-500" />
            <span className="text-sm font-medium text-slate-700">بارگذاری فایل WAV موسیقی انتظار</span>
            <span className="text-xs text-slate-400">{holdHasFile ? 'فایل فعلی موجود است' : 'هنوز فایلی بارگذاری نشده'}</span>
            <input
              ref={holdRef}
              type="file"
              accept=".wav"
              className="hidden"
              onChange={(e) => e.target.files?.[0] && uploadHold(e.target.files[0])}
            />
          </label>
          <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={holdEnabled} onChange={(e) => toggleHold(e.target.checked)} />
            پخش موسیقی انتظار فعال باشد
          </label>
          {hFlash && <span className={`text-sm ${hFlash.ok ? 'text-emerald-600' : 'text-rose-600'}`}>{hFlash.text}</span>}
        </div>
      </Card>
    </div>
  )
}
