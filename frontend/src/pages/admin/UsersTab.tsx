import { useEffect, useState } from 'react'
import { ChevronDown, Pencil, ShieldCheck } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { Button, Card, TextInput, cn } from '../../components/ui'
import { toFa } from '../../lib/format'

interface AdminUser {
  id: number
  phoneNumber: string
  firstName?: string | null
  lastName?: string | null
  brandName?: string | null
  role: string
  callMinuteLimit?: number | null
  usedMinutes: number
  isActive: boolean
  extension?: number | null
}

function UserRow({ user, onSaved }: { user: AdminUser; onSaved: () => void }) {
  const [open, setOpen] = useState(false)
  const [u, setU] = useState(user)
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')

  async function save() {
    setBusy(true)
    setMsg('')
    try {
      await api.put(`/api/admin/users/${user.id}`, {
        firstName: u.firstName,
        lastName: u.lastName,
        brandName: u.brandName,
        isActive: u.isActive,
        callMinuteLimit: u.callMinuteLimit,
      })
      setMsg('ذخیره شد.')
      onSaved()
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rounded-2xl border border-slate-200">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between gap-3 p-4 text-right"
      >
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-sm font-semibold text-slate-800">
            {user.firstName || user.lastName ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() : 'بدون نام'}
            {user.role === 'SuperAdmin' && (
              <span className="flex items-center gap-1 rounded bg-brand-50 px-1.5 py-0.5 text-[10px] text-brand-700">
                <ShieldCheck size={11} /> ادمین
              </span>
            )}
            {!user.isActive && (
              <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[10px] text-rose-600">غیرفعال</span>
            )}
          </div>
          <div className="mt-0.5 truncate text-xs text-slate-400">
            {user.brandName || '—'} · <span dir="ltr">{toFa(user.phoneNumber)}</span> · داخلی{' '}
            {user.extension != null ? toFa(user.extension) : '—'}
          </div>
        </div>
        <ChevronDown size={18} className={cn('shrink-0 text-slate-400 transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <div className="border-t border-slate-100 p-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <TextInput label="نام" value={u.firstName ?? ''} onChange={(e) => setU({ ...u, firstName: e.target.value })} />
            <TextInput label="نام خانوادگی" value={u.lastName ?? ''} onChange={(e) => setU({ ...u, lastName: e.target.value })} />
            <div className="sm:col-span-2">
              <TextInput label="نام برند" value={u.brandName ?? ''} onChange={(e) => setU({ ...u, brandName: e.target.value })} />
            </div>
            <TextInput
              label="محدودیت مکالمه (دقیقه) — خالی = پیش‌فرض"
              type="number"
              value={u.callMinuteLimit ?? ''}
              onChange={(e) => setU({ ...u, callMinuteLimit: e.target.value === '' ? null : Number(e.target.value) })}
            />
            <div className="flex items-end">
              <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={u.isActive} onChange={(e) => setU({ ...u, isActive: e.target.checked })} />
                حساب فعال باشد
              </label>
            </div>
          </div>
          <div className="mt-4 flex items-center gap-4">
            <Button onClick={save} loading={busy}>
              <Pencil size={15} /> ذخیره تغییرات
            </Button>
            {msg && <span className="text-sm text-emerald-600">{msg}</span>}
          </div>
        </div>
      )}
    </div>
  )
}

export default function UsersTab() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)

  async function load() {
    const { data } = await api.get<AdminUser[]>('/api/admin/users')
    setUsers(data)
    setLoading(false)
  }
  useEffect(() => {
    load()
  }, [])

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">کاربران</h3>
      <p className="mt-1 text-sm text-slate-500">
        روی هر کاربر بزنید تا نام، نام‌خانوادگی، برند، وضعیت فعال و محدودیت مکالمه‌ی او را ویرایش کنید.
      </p>

      <div className="mt-5 space-y-2">
        {loading && <p className="text-sm text-slate-400">در حال بارگذاری…</p>}
        {!loading && users.length === 0 && <p className="text-sm text-slate-400">کاربری یافت نشد.</p>}
        {users.map((u) => (
          <UserRow key={u.id} user={u} onSaved={load} />
        ))}
      </div>
    </Card>
  )
}
