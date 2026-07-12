import { useEffect, useState } from 'react'
import { api, apiError } from '../../lib/api'
import { Button, Card, TextInput, cn } from '../../components/ui'
import { toFa } from '../../lib/format'

interface Demo {
  id: number
  label?: string | null
  extension?: number | null
  status: string
  welcomeText?: string | null
  kbText?: string | null
  voiceName?: string | null
  callMinuteLimit?: number | null
  usedMinutes: number
  isActive: boolean
}
interface Voice {
  name: string
  displayName: string
}

function VoiceSelect({ value, onChange, voices }: { value: string; onChange: (v: string) => void; voices: Voice[] }) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="h-12 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm outline-none focus:border-brand-400"
    >
      <option value="">پیش‌فرض</option>
      {voices.map((v) => (
        <option key={v.name} value={v.name}>
          {v.displayName} ({v.name})
        </option>
      ))}
    </select>
  )
}

function DemoRow({ demo, voices, onChanged }: { demo: Demo; voices: Voice[]; onChanged: () => void }) {
  const [d, setD] = useState(demo)
  const [busy, setBusy] = useState(false)

  async function save() {
    setBusy(true)
    try {
      await api.put(`/api/admin/demos/${demo.id}`, {
        label: d.label,
        welcomeText: d.welcomeText,
        kbText: d.kbText,
        voice: d.voiceName,
        minuteLimit: d.callMinuteLimit,
        isActive: d.isActive,
      })
      onChanged()
    } finally {
      setBusy(false)
    }
  }
  async function remove() {
    if (!confirm(`دموی «${demo.label}» حذف شود؟`)) return
    setBusy(true)
    try {
      await api.delete(`/api/admin/demos/${demo.id}`)
      onChanged()
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rounded-2xl border border-slate-200 p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-50 text-lg">🧪</span>
          <div>
            <div className="text-sm font-bold text-slate-800">{demo.label}</div>
            <div className="text-xs text-slate-400">
              داخلی {demo.extension != null ? toFa(demo.extension) : '—'} · {demo.status} · مصرف {toFa(demo.usedMinutes)} دقیقه
            </div>
          </div>
        </div>
        <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-600">
          <input type="checkbox" checked={d.isActive} onChange={(e) => setD({ ...d, isActive: e.target.checked })} />
          فعال
        </label>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <TextInput label="نام دمو" value={d.label ?? ''} onChange={(e) => setD({ ...d, label: e.target.value })} />
        <TextInput
          label="محدودیت مکالمه (دقیقه)"
          type="number"
          value={d.callMinuteLimit ?? ''}
          onChange={(e) => setD({ ...d, callMinuteLimit: e.target.value === '' ? null : Number(e.target.value) })}
        />
        <div>
          <span className="mb-1.5 block text-sm font-medium text-slate-700">گوینده</span>
          <VoiceSelect value={d.voiceName ?? ''} onChange={(v) => setD({ ...d, voiceName: v })} voices={voices} />
        </div>
        <div className="sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-slate-700">پیام خوش‌آمد</span>
          <textarea
            rows={2}
            value={d.welcomeText ?? ''}
            onChange={(e) => setD({ ...d, welcomeText: e.target.value })}
            className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400"
          />
        </div>
        <div className="sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-slate-700">پایگاه دانش</span>
          <textarea
            rows={3}
            value={d.kbText ?? ''}
            onChange={(e) => setD({ ...d, kbText: e.target.value })}
            className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400"
          />
        </div>
      </div>

      <div className="mt-3 flex gap-3">
        <Button onClick={save} loading={busy}>
          ذخیره
        </Button>
        <Button variant="danger" onClick={remove} loading={busy}>
          حذف
        </Button>
      </div>
    </div>
  )
}

export default function DemosTab() {
  const [demos, setDemos] = useState<Demo[]>([])
  const [voices, setVoices] = useState<Voice[]>([])
  const [creating, setCreating] = useState(false)
  const [msg, setMsg] = useState('')
  const [form, setForm] = useState({ label: '', welcomeText: '', kbText: '', voice: '', minuteLimit: '' })

  async function load() {
    const { data } = await api.get<Demo[]>('/api/admin/demos')
    setDemos(data)
  }
  useEffect(() => {
    load()
    api.get<{ voices: Voice[] }>('/api/voices').then(({ data }) => setVoices(data.voices))
  }, [])

  async function create() {
    setMsg('')
    if (!form.label.trim()) return setMsg('نام دمو الزامی است.')
    setCreating(true)
    try {
      await api.post('/api/admin/demos', {
        label: form.label,
        welcomeText: form.welcomeText,
        kbText: form.kbText,
        voice: form.voice || null,
        minuteLimit: form.minuteLimit === '' ? null : Number(form.minuteLimit),
      })
      setForm({ label: '', welcomeText: '', kbText: '', voice: '', minuteLimit: '' })
      setMsg('دمو ساخته شد.')
      await load()
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setCreating(false)
    }
  }

  return (
    <div className="space-y-6">
      <Card className="animate-in">
        <h3 className="text-lg font-bold text-slate-800">ساخت دمو جدید</h3>
        <p className="mt-1 text-sm text-slate-500">
          هر دمو یک داخلی در بازه‌ی ۱ تا ۹۹۹ می‌گیرد. تعداد دموها نامحدود است.
        </p>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          <TextInput label="نام دمو" value={form.label} onChange={(e) => setForm({ ...form, label: e.target.value })} />
          <TextInput
            label="محدودیت مکالمه (دقیقه)"
            type="number"
            value={form.minuteLimit}
            onChange={(e) => setForm({ ...form, minuteLimit: e.target.value })}
          />
          <div className="sm:col-span-2">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">گوینده</span>
            <VoiceSelect value={form.voice} onChange={(v) => setForm({ ...form, voice: v })} voices={voices} />
          </div>
          <div className="sm:col-span-2">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">پیام خوش‌آمد</span>
            <textarea
              rows={2}
              value={form.welcomeText}
              onChange={(e) => setForm({ ...form, welcomeText: e.target.value })}
              className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400"
            />
          </div>
          <div className="sm:col-span-2">
            <span className="mb-1.5 block text-sm font-medium text-slate-700">پایگاه دانش</span>
            <textarea
              rows={3}
              value={form.kbText}
              onChange={(e) => setForm({ ...form, kbText: e.target.value })}
              className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400"
            />
          </div>
        </div>
        <div className="mt-4 flex items-center gap-4">
          <Button onClick={create} loading={creating}>
            ساخت دمو
          </Button>
          {msg && <span className={cn('text-sm', msg.includes('ساخته') ? 'text-emerald-600' : 'text-rose-600')}>{msg}</span>}
        </div>
      </Card>

      <div className="space-y-3">
        <h3 className="text-lg font-bold text-slate-800">دموهای موجود ({toFa(demos.length)})</h3>
        {demos.length === 0 && <p className="text-sm text-slate-400">هنوز دمویی ساخته نشده است.</p>}
        {demos.map((d) => (
          <DemoRow key={d.id} demo={d} voices={voices} onChanged={load} />
        ))}
      </div>
    </div>
  )
}
