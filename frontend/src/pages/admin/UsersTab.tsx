import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import { Card } from '../../components/ui'
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

export default function UsersTab() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [saving, setSaving] = useState<number | null>(null)

  async function load() {
    const { data } = await api.get<AdminUser[]>('/api/admin/users')
    setUsers(data)
  }
  useEffect(() => {
    load()
  }, [])

  async function saveLimit(u: AdminUser, value: string) {
    setSaving(u.id)
    try {
      const limit = value.trim() === '' ? null : Number(value)
      await api.put(`/api/admin/users/${u.id}/limit`, { callMinuteLimit: limit })
      await load()
    } finally {
      setSaving(null)
    }
  }

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">کاربران و محدودیت‌ها</h3>
      <p className="mt-1 text-sm text-slate-500">محدودیت مکالمه‌ی هر کاربر (دقیقه) را می‌توانید تغییر دهید. خالی = پیش‌فرض سامانه.</p>

      <div className="mt-5 overflow-x-auto">
        <table className="w-full min-w-[640px] text-right text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-xs text-slate-400">
              <th className="p-3 font-medium">کاربر</th>
              <th className="p-3 font-medium">موبایل</th>
              <th className="p-3 font-medium">داخلی</th>
              <th className="p-3 font-medium">مصرف (دقیقه)</th>
              <th className="p-3 font-medium">سقف اختصاصی</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id} className="border-b border-slate-100">
                <td className="p-3">
                  <div className="font-medium text-slate-800">
                    {u.firstName} {u.lastName}
                    {u.role === 'SuperAdmin' && (
                      <span className="mr-2 rounded bg-brand-50 px-1.5 py-0.5 text-[10px] text-brand-700">ادمین</span>
                    )}
                  </div>
                  <div className="text-xs text-slate-400">{u.brandName}</div>
                </td>
                <td className="p-3 text-slate-600" dir="ltr">
                  {toFa(u.phoneNumber)}
                </td>
                <td className="p-3 text-slate-600">{u.extension ? toFa(u.extension) : '—'}</td>
                <td className="p-3 text-slate-600">{toFa(u.usedMinutes)}</td>
                <td className="p-3">
                  <input
                    type="number"
                    defaultValue={u.callMinuteLimit ?? ''}
                    placeholder="پیش‌فرض"
                    disabled={saving === u.id}
                    onBlur={(e) => saveLimit(u, e.target.value)}
                    className="h-9 w-28 rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-brand-400"
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  )
}
