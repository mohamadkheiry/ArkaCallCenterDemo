import { useEffect, useState } from 'react'
import { api, apiError } from '../../lib/api'
import { useFlash } from '../../lib/flash'
import { Button, Card } from '../../components/ui'

interface Voice {
  name: string
  displayName: string
}

export default function FallbackTab() {
  const [text, setText] = useState('')
  const [voice, setVoice] = useState('alloy')
  const [voices, setVoices] = useState<Voice[]>([])
  const [hasAudio, setHasAudio] = useState(false)
  const [busy, setBusy] = useState(false)
  const { flash, ok, fail, clear } = useFlash()

  useEffect(() => {
    api.get('/api/admin/fallback-message').then(({ data }) => {
      setText(data.text ?? '')
      setVoice(data.voice ?? 'alloy')
      setHasAudio(!!data.hasAudio)
    })
    api.get<{ voices: Voice[] }>('/api/voices').then(({ data }) => setVoices(data.voices))
  }, [])

  async function save() {
    setBusy(true)
    clear()
    try {
      const { data } = await api.put('/api/admin/fallback-message', { text, voice })
      ok(data.message)
      setHasAudio(!!data.audioGenerated || hasAudio)
    } catch (e) {
      fail(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">پیام «پاسخ در پایگاه دانش نیست»</h3>
      <p className="mt-1 text-sm text-slate-500">
        وقتی پاسخ سوال در پایگاه دانش کاربر یافت نشود، این پیام از پیش‌ساخته پلی می‌شود (به‌جای تولید
        realtime، برای صرفه‌جویی در مصرف توکن).
      </p>

      <div className="mt-5 space-y-4">
        <div>
          <span className="mb-1.5 block text-sm font-medium text-slate-700">متن پیام</span>
          <textarea
            rows={3}
            value={text}
            onChange={(e) => setText(e.target.value)}
            className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
          />
        </div>
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
        <div className="flex items-center gap-3 text-sm">
          <span className={hasAudio ? 'text-emerald-600' : 'text-slate-400'}>
            {hasAudio ? 'فایل صوتی موجود است ✅' : 'هنوز فایل صوتی تولید نشده'}
          </span>
        </div>
      </div>

      <div className="mt-5 flex items-center gap-4">
        <Button onClick={save} loading={busy}>
          ذخیره و تولید صوت
        </Button>
        {flash && <span className={`text-sm ${flash.ok ? 'text-emerald-600' : 'text-rose-600'}`}>{flash.text}</span>}
      </div>
    </Card>
  )
}
