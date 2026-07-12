import { useEffect, useState } from 'react'
import { api } from '../../lib/api'
import { Card, cn } from '../../components/ui'
import { toFa } from '../../lib/format'

interface KeyUsage {
  apiKey: string
  totalTokens: number
  promptTokens: number
  completionTokens: number
  calls: number
  firstUsed: string
  lastUsed: string
}
interface UserUsage {
  userId: number | null
  phoneNumber: string | null
  name: string | null
  brand: string | null
  totalTokens: number
  promptTokens: number
  completionTokens: number
  calls: number
  lastUsed: string
}

/** تاریخ و ساعت شمسی. */
function jalali(iso: string) {
  try {
    return new Date(iso).toLocaleString('fa-IR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return '—'
  }
}

function num(n: number) {
  return toFa(n.toLocaleString('en-US'))
}

export default function UsageTab() {
  const [keys, setKeys] = useState<KeyUsage[]>([])
  const [users, setUsers] = useState<UserUsage[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    Promise.all([
      api.get<KeyUsage[]>('/api/admin/usage/keys'),
      api.get<UserUsage[]>('/api/admin/usage/users'),
    ])
      .then(([k, u]) => {
        setKeys(k.data)
        setUsers(u.data)
      })
      .finally(() => setLoading(false))
  }, [])

  const grandTotal = keys.reduce((s, k) => s + k.totalTokens, 0)

  return (
    <div className="space-y-6">
      <Card className="animate-in">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-bold text-slate-800">مصرف توکن به تفکیک کلید API</h3>
            <p className="mt-1 text-sm text-slate-500">مجموع توکن مصرف‌شده برای هر کلید، با تاریخ شمسی.</p>
          </div>
          <div className="text-left">
            <div className="text-xs text-slate-400">مجموع کل</div>
            <div className="text-xl font-extrabold text-brand-700">{num(grandTotal)}</div>
          </div>
        </div>

        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[720px] text-right text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs text-slate-400">
                <th className="p-3 font-medium">کلید API</th>
                <th className="p-3 font-medium">کل توکن</th>
                <th className="p-3 font-medium">ورودی</th>
                <th className="p-3 font-medium">خروجی</th>
                <th className="p-3 font-medium">تعداد فراخوانی</th>
                <th className="p-3 font-medium">اولین استفاده</th>
                <th className="p-3 font-medium">آخرین استفاده</th>
              </tr>
            </thead>
            <tbody>
              {keys.length === 0 && !loading && (
                <tr>
                  <td colSpan={7} className="p-6 text-center text-slate-400">
                    هنوز مصرفی ثبت نشده است.
                  </td>
                </tr>
              )}
              {keys.map((k) => (
                <tr key={k.apiKey} className="border-b border-slate-100">
                  <td className="p-3 font-mono text-xs text-slate-700" dir="ltr">
                    {k.apiKey}
                  </td>
                  <td className="p-3 font-semibold text-slate-800">{num(k.totalTokens)}</td>
                  <td className="p-3 text-slate-500">{num(k.promptTokens)}</td>
                  <td className="p-3 text-slate-500">{num(k.completionTokens)}</td>
                  <td className="p-3 text-slate-500">{toFa(k.calls)}</td>
                  <td className="p-3 text-slate-500">{jalali(k.firstUsed)}</td>
                  <td className="p-3 text-slate-500">{jalali(k.lastUsed)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <Card className="animate-in">
        <h3 className="text-lg font-bold text-slate-800">مصرف توکن به تفکیک کاربر / شماره موبایل</h3>
        <p className="mt-1 text-sm text-slate-500">مشخص می‌کند هر کاربر چه میزان توکن مصرف کرده است.</p>

        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[640px] text-right text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs text-slate-400">
                <th className="p-3 font-medium">کاربر</th>
                <th className="p-3 font-medium">موبایل</th>
                <th className="p-3 font-medium">کل توکن</th>
                <th className="p-3 font-medium">فراخوانی</th>
                <th className="p-3 font-medium">آخرین استفاده</th>
              </tr>
            </thead>
            <tbody>
              {users.length === 0 && !loading && (
                <tr>
                  <td colSpan={5} className="p-6 text-center text-slate-400">
                    هنوز مصرفی ثبت نشده است.
                  </td>
                </tr>
              )}
              {users.map((u, i) => (
                <tr key={i} className={cn('border-b border-slate-100')}>
                  <td className="p-3">
                    <div className="font-medium text-slate-800">{u.name || (u.userId ? `کاربر #${toFa(u.userId)}` : 'سیستم')}</div>
                    {u.brand && <div className="text-xs text-slate-400">{u.brand}</div>}
                  </td>
                  <td className="p-3 text-slate-600" dir="ltr">
                    {u.phoneNumber ? toFa(u.phoneNumber) : '—'}
                  </td>
                  <td className="p-3 font-semibold text-slate-800">{num(u.totalTokens)}</td>
                  <td className="p-3 text-slate-500">{toFa(u.calls)}</td>
                  <td className="p-3 text-slate-500">{jalali(u.lastUsed)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  )
}
