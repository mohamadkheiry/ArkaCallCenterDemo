import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import { Card, cn } from '../components/ui'
import { toFa } from '../lib/format'

interface CallRow {
  id: number
  callerId?: string | null
  startedAt: string
  durationSeconds: number
  answeredFromKb: boolean
}

function formatDuration(sec: number) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${toFa(m)}:${toFa(s.toString().padStart(2, '0'))}`
}

export default function CallsPage() {
  const [calls, setCalls] = useState<CallRow[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api
      .get<CallRow[]>('/api/calls')
      .then(({ data }) => setCalls(data))
      .finally(() => setLoading(false))
  }, [])

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">تماس‌ها</h1>
        <p className="mt-1 text-sm text-slate-500">تاریخچه‌ی تماس‌های پاسخ‌داده‌شده توسط هوش مصنوعی.</p>
      </div>

      <Card className="animate-in">
        {loading ? (
          <p className="text-sm text-slate-400">در حال بارگذاری…</p>
        ) : calls.length === 0 ? (
          <div className="py-10 text-center">
            <div className="mx-auto mb-3 grid h-14 w-14 place-items-center rounded-2xl bg-slate-50 text-2xl">📭</div>
            <p className="text-sm text-slate-500">هنوز تماسی ثبت نشده است.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[560px] text-right text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-xs text-slate-400">
                  <th className="p-3 font-medium">تماس‌گیرنده</th>
                  <th className="p-3 font-medium">زمان</th>
                  <th className="p-3 font-medium">مدت</th>
                  <th className="p-3 font-medium">نوع پاسخ</th>
                </tr>
              </thead>
              <tbody>
                {calls.map((c) => (
                  <tr key={c.id} className="border-b border-slate-100">
                    <td className="p-3 text-slate-700" dir="ltr">
                      {c.callerId ? toFa(c.callerId) : 'نامشخص'}
                    </td>
                    <td className="p-3 text-slate-500">{new Date(c.startedAt).toLocaleString('fa-IR')}</td>
                    <td className="p-3 text-slate-600">{formatDuration(c.durationSeconds)}</td>
                    <td className="p-3">
                      <span
                        className={cn(
                          'rounded-full px-2.5 py-1 text-xs',
                          c.answeredFromKb ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700',
                        )}
                      >
                        {c.answeredFromKb ? 'از پایگاه دانش' : 'خارج از پایگاه دانش'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  )
}
