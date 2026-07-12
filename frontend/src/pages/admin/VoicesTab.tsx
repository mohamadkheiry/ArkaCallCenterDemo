import { useEffect, useState } from 'react'
import { api, apiError } from '../../lib/api'
import { Button, Card, cn } from '../../components/ui'

interface Voice {
  name: string
  displayName: string
  enabled: boolean
  isDefault: boolean
}

export default function VoicesTab() {
  const [voices, setVoices] = useState<Voice[]>([])
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')

  useEffect(() => {
    api.get<Voice[]>('/api/admin/voices').then(({ data }) => setVoices(data))
  }, [])

  function update(name: string, patch: Partial<Voice>) {
    setVoices((vs) => vs.map((v) => (v.name === name ? { ...v, ...patch } : v)))
  }
  function setDefault(name: string) {
    setVoices((vs) => vs.map((v) => ({ ...v, isDefault: v.name === name, enabled: v.name === name ? true : v.enabled })))
  }

  async function save() {
    setBusy(true)
    setMsg('')
    try {
      await api.put('/api/admin/voices', { voices })
      setMsg('گوینده‌ها ذخیره شد.')
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">مدیریت گوینده‌ها</h3>
      <p className="mt-1 text-sm text-slate-500">گوینده‌های در دسترس کاربران و گوینده‌ی پیش‌فرض سامانه.</p>

      <div className="mt-5 space-y-2">
        {voices.map((v) => (
          <div key={v.name} className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 p-3">
            <input
              value={v.displayName}
              onChange={(e) => update(v.name, { displayName: e.target.value })}
              className="h-9 flex-1 rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-brand-400"
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
          </div>
        ))}
      </div>

      <div className="mt-5 flex items-center gap-4">
        <Button onClick={save} loading={busy}>
          ذخیره
        </Button>
        {msg && <span className="text-sm text-emerald-600">{msg}</span>}
      </div>
    </Card>
  )
}
