import { useEffect, useRef, useState } from 'react'
import { AudioLines, Sparkles, Upload } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { useFlash } from '../../lib/flash'
import { Button, Card, cn } from '../../components/ui'
import VoiceSampleButton from '../../components/VoiceSampleButton'

interface Voice {
  name: string
  displayName: string
  enabled: boolean
  isDefault: boolean
  hasSample: boolean
}

export default function VoicesTab() {
  const [voices, setVoices] = useState<Voice[]>([])
  const [sampleText, setSampleText] = useState('')
  const [busy, setBusy] = useState(false)
  const [sampleBusy, setSampleBusy] = useState<string | null>(null)
  const { flash, ok, fail, clear } = useFlash()
  const uploadRef = useRef<HTMLInputElement>(null)
  const [uploadTarget, setUploadTarget] = useState<string | null>(null)

  async function load() {
    const { data } = await api.get<{ sampleText: string; voices: Voice[] }>('/api/admin/voices')
    setVoices(data.voices)
    setSampleText(data.sampleText ?? '')
  }
  useEffect(() => {
    load()
  }, [])

  function update(name: string, patch: Partial<Voice>) {
    setVoices((vs) => vs.map((v) => (v.name === name ? { ...v, ...patch } : v)))
  }
  function setDefault(name: string) {
    setVoices((vs) => vs.map((v) => ({ ...v, isDefault: v.name === name, enabled: v.name === name ? true : v.enabled })))
  }

  async function save() {
    setBusy(true)
    clear()
    try {
      await api.put('/api/admin/voices', { voices })
      ok('گوینده‌ها ذخیره شد.')
    } catch (e) {
      fail(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  async function generateSample(name: string) {
    setSampleBusy(name)
    clear()
    try {
      const { data } = await api.post(`/api/admin/voices/${name}/sample-generate`, { text: sampleText })
      ok(data.message)
      await load()
    } catch (e) {
      fail(apiError(e))
    } finally {
      setSampleBusy(null)
    }
  }

  function pickUpload(name: string) {
    setUploadTarget(name)
    uploadRef.current?.click()
  }

  async function uploadSample(file: File) {
    if (!uploadTarget) return
    setSampleBusy(uploadTarget)
    clear()
    try {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post(`/api/admin/voices/${uploadTarget}/sample-file`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      ok(data.message)
      await load()
    } catch (e) {
      fail(apiError(e))
    } finally {
      setSampleBusy(null)
      setUploadTarget(null)
      if (uploadRef.current) uploadRef.current.value = ''
    }
  }

  return (
    <Card className="animate-in">
      <div className="flex items-center gap-2">
        <AudioLines size={19} className="text-brand-600" />
        <h3 className="text-lg font-bold text-slate-800">مدیریت گوینده‌ها</h3>
      </div>
      <p className="mt-1 text-sm text-slate-500">
        گوینده‌های در دسترس کاربران، گوینده‌ی پیش‌فرض، و نمونه‌صدای هرکدام (برای پیش‌نمایش کاربر).
      </p>

      <div className="mt-4">
        <span className="mb-1.5 block text-sm font-medium text-slate-700">متن نمونه‌صدا (برای تولید با هوش مصنوعی)</span>
        <textarea
          rows={2}
          value={sampleText}
          onChange={(e) => setSampleText(e.target.value)}
          className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
        />
      </div>

      {/* input مخفی برای آپلود mp3 */}
      <input
        ref={uploadRef}
        type="file"
        accept=".mp3"
        className="hidden"
        onChange={(e) => e.target.files?.[0] && uploadSample(e.target.files[0])}
      />

      <div className="mt-4 space-y-2">
        {voices.map((v) => (
          <div key={v.name} className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 p-3">
            <VoiceSampleButton voiceName={v.name} hasSample={v.hasSample} />
            <input
              value={v.displayName}
              onChange={(e) => update(v.name, { displayName: e.target.value })}
              className="h-9 min-w-32 flex-1 rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-brand-400"
            />
            <span className="text-xs text-slate-400" dir="ltr">
              {v.name}
            </span>
            <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-600">
              <input type="checkbox" checked={v.enabled} onChange={(e) => update(v.name, { enabled: e.target.checked })} />
              فعال
            </label>
            <button
              onClick={() => setDefault(v.name)}
              className={cn(
                'rounded-lg px-3 py-1.5 text-xs font-medium transition-colors',
                v.isDefault ? 'bg-brand-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
              )}
            >
              {v.isDefault ? 'پیش‌فرض ✓' : 'تنظیم پیش‌فرض'}
            </button>
            <button
              onClick={() => generateSample(v.name)}
              disabled={sampleBusy !== null}
              className="flex items-center gap-1.5 rounded-lg bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-50"
              title="تولید نمونه‌صدا با هوش مصنوعی"
            >
              <Sparkles size={13} />
              {sampleBusy === v.name ? 'در حال تولید…' : v.hasSample ? 'تولید مجدد نمونه' : 'تولید نمونه'}
            </button>
            <button
              onClick={() => pickUpload(v.name)}
              disabled={sampleBusy !== null}
              className="flex items-center gap-1.5 rounded-lg bg-slate-100 px-3 py-1.5 text-xs font-medium text-slate-600 transition-colors hover:bg-slate-200 disabled:opacity-50"
              title="آپلود فایل mp3 نمونه"
            >
              <Upload size={13} />
              آپلود
            </button>
          </div>
        ))}
      </div>

      <div className="mt-5 flex items-center gap-4">
        <Button onClick={save} loading={busy}>
          ذخیره
        </Button>
        {flash && <span className={cn('text-sm', flash.ok ? 'text-emerald-600' : 'text-rose-600')}>{flash.text}</span>}
      </div>
    </Card>
  )
}
