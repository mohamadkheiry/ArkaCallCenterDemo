import { useEffect, useState } from 'react'
import { api, apiError } from '../../lib/api'
import { Button, Card, TextInput } from '../../components/ui'
import { SETTING_FIELDS } from './adminData'

export default function SettingsTab({ groups, title, desc }: { groups: string[]; title: string; desc: string }) {
  const fields = SETTING_FIELDS.filter((f) => groups.includes(f.group))
  const [values, setValues] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')

  useEffect(() => {
    api.get<Record<string, string | null>>('/api/admin/settings').then(({ data }) => {
      const next: Record<string, string> = {}
      for (const f of fields) next[f.key] = data[f.key] ?? ''
      setValues(next)
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [title])

  async function save() {
    setBusy(true)
    setMsg('')
    try {
      await api.put('/api/admin/settings', { settings: values })
      setMsg('تنظیمات ذخیره شد.')
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">{title}</h3>
      <p className="mt-1 text-sm text-slate-500">{desc}</p>
      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        {fields.map((f) => (
          <TextInput
            key={f.key}
            label={f.label}
            hint={f.hint}
            type={f.secret ? 'password' : 'text'}
            dir={f.secret || f.key.includes('Url') || f.key.includes('Model') ? 'ltr' : 'rtl'}
            placeholder={f.secret ? '••••••••' : ''}
            value={values[f.key] ?? ''}
            onChange={(e) => setValues((v) => ({ ...v, [f.key]: e.target.value }))}
          />
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
