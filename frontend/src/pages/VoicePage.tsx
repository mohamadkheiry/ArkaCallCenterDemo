import { useEffect, useState } from 'react'
import { Check, Mic } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { useFlash } from '../lib/flash'
import { useAuth } from '../context/AuthContext'
import { Button, cn } from '../components/ui'
import VoiceSampleButton from '../components/VoiceSampleButton'

interface Voice {
  name: string
  displayName: string
  isDefault: boolean
  hasSample: boolean
}

export default function VoicePage() {
  const { me, refresh } = useAuth()
  const [voices, setVoices] = useState<Voice[]>([])
  const [selected, setSelected] = useState<string>('')
  const [defaultVoice, setDefaultVoice] = useState<string>('')
  const [busy, setBusy] = useState(false)
  const { flash, ok, fail, clear } = useFlash()

  useEffect(() => {
    api.get<{ voices: Voice[]; defaultVoice: string }>('/api/voices').then(({ data }) => {
      setVoices(data.voices)
      setDefaultVoice(data.defaultVoice)
      setSelected(me?.voiceName ?? data.defaultVoice)
    })
  }, [me?.voiceName])

  async function save() {
    setBusy(true)
    clear()
    try {
      await api.put('/api/me/voice', { voiceName: selected })
      await refresh()
      ok('گوینده ذخیره شد.')
    } catch (e) {
      fail(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">صدای گوینده</h1>
        <p className="mt-1 text-sm text-slate-500">
          گوینده‌ای که هوش مصنوعی با آن به تماس‌گیرندگان پاسخ می‌دهد را انتخاب کنید.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        {voices.map((v) => (
          <button
            key={v.name}
            onClick={() => setSelected(v.name)}
            className={cn(
              'flex items-center justify-between rounded-2xl border p-4 text-right transition-all',
              selected === v.name
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
                <div className="text-xs text-slate-400" dir="ltr">
                  {v.name}
                  {v.name === defaultVoice ? ' · پیش‌فرض' : ''}
                </div>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <VoiceSampleButton voiceName={v.name} hasSample={v.hasSample} />
              <span
                className={cn(
                  'grid h-6 w-6 place-items-center rounded-full border',
                  selected === v.name ? 'border-brand-500 bg-brand-600 text-white' : 'border-slate-300 text-transparent',
                )}
              >
                <Check size={13} />
              </span>
            </div>
          </button>
        ))}
      </div>

      <div className="flex items-center gap-4">
        <Button onClick={save} loading={busy}>
          ذخیره گوینده
        </Button>
        {flash && <span className={cn('text-sm', flash.ok ? 'text-emerald-600' : 'text-rose-600')}>{flash.text}</span>}
      </div>
    </div>
  )
}
