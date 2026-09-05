import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ChevronDown,
  Pencil,
  ShieldCheck,
  LogIn,
  Search,
  UserPlus,
} from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { useFlash } from '../../lib/flash'
import { useAuth } from '../../context/AuthContext'
import { Button, Card, Skeleton, TextInput, cn } from '../../components/ui'
import { toFa, toEn } from '../../lib/format'

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
  const { flash, ok, fail, clear } = useFlash()
  const [entering, setEntering] = useState(false)
  const [promoting, setPromoting] = useState(false)
  const { impersonate } = useAuth()
  const navigate = useNavigate()

  async function enterPanel() {
    setEntering(true)
    clear()
    try {
      const { data } = await api.post<{ token: string; name: string }>(
        `/api/admin/users/${user.id}/impersonate`,
      )
      impersonate(data.token, data.name)
      navigate('/', { replace: true })
    } catch (e) {
      fail(apiError(e))
      setEntering(false)
    }
  }

  async function save() {
    setBusy(true)
    clear()
    try {
      await api.put(`/api/admin/users/${user.id}`, {
        firstName: u.firstName,
        lastName: u.lastName,
        brandName: u.brandName,
        isActive: u.isActive,
        callMinuteLimit: u.callMinuteLimit,
      })
      ok('ذخیره شد.')
      onSaved()
    } catch (e) {
      fail(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  async function promoteToSuperAdmin() {
    const label =
      `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() ||
      user.phoneNumber
    if (
      !confirm(
        `نقش «${label}» به سوپرادمین تغییر کند؟ این کاربر به تمام بخش‌های مدیریتی دسترسی خواهد داشت.`,
      )
    )
      return
    setPromoting(true)
    clear()
    try {
      await api.put(`/api/admin/users/${user.id}/role`, { role: 'SuperAdmin' })
      ok(
        'کاربر به سوپرادمین ارتقا یافت. برای دریافت دسترسی جدید باید دوباره وارد سامانه شود.',
      )
      onSaved()
    } catch (e) {
      fail(apiError(e))
    } finally {
      setPromoting(false)
    }
  }

  return (
    <div className="admin-user-row">
      <button
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="flex w-full items-center justify-between gap-3 p-4 text-right"
      >
        <span className="admin-user-avatar">{user.firstName?.[0] || 'ک'}</span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 text-sm font-semibold text-slate-800">
            {user.firstName || user.lastName
              ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim()
              : 'بدون نام'}
            {user.role === 'SuperAdmin' && (
              <span className="flex items-center gap-1 rounded bg-brand-50 px-1.5 py-0.5 text-[10px] text-brand-700">
                <ShieldCheck size={11} /> ادمین
              </span>
            )}
            {!user.isActive && (
              <span className="rounded bg-rose-50 px-1.5 py-0.5 text-[10px] text-rose-600">
                غیرفعال
              </span>
            )}
          </div>
          <div className="mt-0.5 truncate text-xs text-slate-400">
            {user.brandName || '—'} ·{' '}
            <span dir="ltr">{toFa(user.phoneNumber)}</span> · داخلی{' '}
            {user.extension != null ? toFa(user.extension) : '—'}
          </div>
        </div>
        <ChevronDown
          size={18}
          className={cn(
            'shrink-0 text-slate-400 transition-transform',
            open && 'rotate-180',
          )}
        />
      </button>

      {open && (
        <div className="border-t border-slate-100 p-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <TextInput
              label="نام"
              value={u.firstName ?? ''}
              onChange={(e) => setU({ ...u, firstName: e.target.value })}
            />
            <TextInput
              label="نام خانوادگی"
              value={u.lastName ?? ''}
              onChange={(e) => setU({ ...u, lastName: e.target.value })}
            />
            <div className="sm:col-span-2">
              <TextInput
                label="نام برند"
                value={u.brandName ?? ''}
                onChange={(e) => setU({ ...u, brandName: e.target.value })}
              />
            </div>
            <TextInput
              label="محدودیت مکالمه (دقیقه) — خالی = پیش‌فرض"
              type="number"
              value={u.callMinuteLimit ?? ''}
              onChange={(e) =>
                setU({
                  ...u,
                  callMinuteLimit:
                    e.target.value === '' ? null : Number(e.target.value),
                })
              }
            />
            <div className="flex items-end">
              <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
                <input
                  type="checkbox"
                  checked={u.isActive}
                  onChange={(e) => setU({ ...u, isActive: e.target.checked })}
                />
                حساب فعال باشد
              </label>
            </div>
          </div>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Button onClick={save} loading={busy}>
              <Pencil size={15} /> ذخیره تغییرات
            </Button>
            {user.role !== 'SuperAdmin' && (
              <>
                <Button
                  variant="outline"
                  onClick={enterPanel}
                  loading={entering}
                >
                  <LogIn size={15} /> ورود به پنل کاربر
                </Button>
                <Button onClick={promoteToSuperAdmin} loading={promoting}>
                  <ShieldCheck size={15} /> ارتقا به سوپرادمین
                </Button>
              </>
            )}
            {flash && (
              <span
                className={cn(
                  'text-sm',
                  flash.ok ? 'text-emerald-600' : 'text-rose-600',
                )}
              >
                {flash.text}
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

export default function UsersTab() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)
  const [q, setQ] = useState('')
  const [newAdmin, setNewAdmin] = useState({
    phoneNumber: '',
    firstName: '',
    lastName: '',
  })
  const [creatingAdmin, setCreatingAdmin] = useState(false)
  const { flash, ok, fail, clear } = useFlash()

  async function load() {
    const { data } = await api.get<AdminUser[]>('/api/admin/users')
    setUsers(data)
    setLoading(false)
  }
  useEffect(() => {
    load()
  }, [])

  async function createSuperAdmin(e: React.FormEvent) {
    e.preventDefault()
    clear()
    setCreatingAdmin(true)
    try {
      const phoneNumber = toEn(newAdmin.phoneNumber).replace(/\D/g, '')
      const { data } = await api.post<{ message: string }>(
        '/api/admin/users/super-admins',
        {
          phoneNumber,
          firstName: newAdmin.firstName || null,
          lastName: newAdmin.lastName || null,
        },
      )
      ok(data.message)
      setNewAdmin({ phoneNumber: '', firstName: '', lastName: '' })
      await load()
    } catch (e) {
      fail(apiError(e))
    } finally {
      setCreatingAdmin(false)
    }
  }

  // سرچ زنده روی نام، نام‌خانوادگی، برند، شماره و داخلی
  const filtered = useMemo(() => {
    // ارقامِ فارسیِ ورودی را به انگلیسی نرمال کن تا جستجوی شماره/داخلی هم کار کند.
    const term = toEn(q.trim()).toLowerCase()
    if (!term) return users
    return users.filter((u) =>
      [
        u.firstName,
        u.lastName,
        u.brandName,
        u.phoneNumber,
        u.extension?.toString(),
      ].some((f) => f?.toLowerCase().includes(term)),
    )
  }, [users, q])

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">کاربران</h3>
      <p className="mt-1 text-sm text-slate-500">
        کاربران را ویرایش کنید، وارد پنلشان شوید یا دسترسی سوپرادمین بدهید.
      </p>

      <form
        onSubmit={createSuperAdmin}
        className="mt-5 rounded-2xl border border-brand-100 bg-brand-50/50 p-4"
      >
        <div className="flex items-center gap-2 text-sm font-bold text-slate-800">
          <UserPlus size={18} className="text-brand-600" /> ایجاد سوپرادمین جدید
        </div>
        <p className="mt-1 text-xs leading-6 text-slate-500">
          اگر شماره قبلاً ثبت شده باشد، همان کاربر ارتقا پیدا می‌کند؛ در غیر این
          صورت حساب جدید ساخته می‌شود. ورود با OTP انجام می‌شود.
        </p>
        <div className="mt-3 grid gap-3 md:grid-cols-3">
          <TextInput
            label="شماره موبایل"
            dir="ltr"
            inputMode="numeric"
            required
            placeholder="09123456789"
            value={newAdmin.phoneNumber}
            onChange={(e) =>
              setNewAdmin({ ...newAdmin, phoneNumber: e.target.value })
            }
          />
          <TextInput
            label="نام (اختیاری)"
            value={newAdmin.firstName}
            onChange={(e) =>
              setNewAdmin({ ...newAdmin, firstName: e.target.value })
            }
          />
          <TextInput
            label="نام خانوادگی (اختیاری)"
            value={newAdmin.lastName}
            onChange={(e) =>
              setNewAdmin({ ...newAdmin, lastName: e.target.value })
            }
          />
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <Button type="submit" loading={creatingAdmin}>
            <ShieldCheck size={16} /> ایجاد یا ارتقا
          </Button>
          {flash && (
            <span
              className={cn(
                'text-sm',
                flash.ok ? 'text-emerald-600' : 'text-rose-600',
              )}
            >
              {flash.text}
            </span>
          )}
        </div>
      </form>

      <div className="relative mt-4">
        <Search
          size={17}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400"
        />
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="جست‌وجوی زنده: نام، برند، شماره یا داخلی…"
          className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pr-10 pl-3 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100"
        />
      </div>

      <div className="mt-4 space-y-2">
        {loading &&
          Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="flex items-center gap-3 rounded-xl border border-slate-200/60 bg-white/85 p-4 shadow-soft"
            >
              <Skeleton className="h-10 w-10 rounded-full" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-3.5 w-40" />
                <Skeleton className="h-3 w-24" />
              </div>
              <Skeleton className="h-8 w-20 rounded-lg" />
            </div>
          ))}
        {!loading && filtered.length === 0 && (
          <p className="text-sm text-slate-400">کاربری یافت نشد.</p>
        )}
        {filtered.map((u) => (
          <UserRow key={u.id} user={u} onSaved={load} />
        ))}
      </div>
    </Card>
  )
}
