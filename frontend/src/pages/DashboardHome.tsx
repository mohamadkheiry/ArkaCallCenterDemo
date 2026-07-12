import { Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { Button, Card } from '../components/ui'
import { toFa } from '../lib/format'

function StatCard({ title, value, sub, icon }: { title: string; value: string; sub?: string; icon: string }) {
  return (
    <Card className="animate-in">
      <div className="flex items-start justify-between">
        <div>
          <div className="text-sm text-slate-500">{title}</div>
          <div className="mt-2 text-2xl font-extrabold text-slate-800">{value}</div>
          {sub && <div className="mt-1 text-xs text-slate-400">{sub}</div>}
        </div>
        <div className="grid h-11 w-11 place-items-center rounded-xl bg-brand-50 text-xl">{icon}</div>
      </div>
    </Card>
  )
}

export default function DashboardHome() {
  const { me } = useAuth()
  const sp = me?.smartPhone
  const limit = me?.callMinuteLimit ?? null

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">داشبورد</h1>
        <p className="mt-1 text-sm text-slate-500">وضعیت تلفن هوشمند و پایگاه دانش شما.</p>
      </div>

      {!sp && (
        <Card className="animate-in border-brand-200 bg-gradient-to-l from-brand-50 to-white">
          <div className="flex flex-col items-start justify-between gap-4 sm:flex-row sm:items-center">
            <div>
              <h3 className="text-lg font-bold text-slate-800">هنوز تلفن هوشمندی نساخته‌اید</h3>
              <p className="mt-1 text-sm text-slate-500">
                پایگاه دانش و پیام خوش‌آمد را تنظیم کنید و تلفن هوشمند خود را بسازید.
              </p>
            </div>
            <Link to="/knowledge-base">
              <Button>شروع ساخت تلفن هوشمند</Button>
            </Link>
          </div>
        </Card>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard
          title="شماره داخلی"
          value={sp ? toFa(sp.extension) : '—'}
          sub={sp ? `وضعیت: ${sp.status}` : 'ساخته نشده'}
          icon="☎️"
        />
        <StatCard
          title="دقایق مصرف‌شده"
          value={toFa(me?.usedMinutes ?? 0)}
          sub={limit ? `از ${toFa(limit)} دقیقه` : 'محدودیت پیش‌فرض'}
          icon="⏱️"
        />
        <StatCard
          title="گوینده"
          value={me?.voiceName ?? 'پیش‌فرض'}
          sub="قابل تغییر در بخش صدا"
          icon="🎙️"
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="animate-in">
          <h3 className="font-bold text-slate-800">راهنمای سریع</h3>
          <ol className="mt-3 space-y-2 text-sm text-slate-600">
            <li>۱. پایگاه دانش خود را وارد کنید (متن یا فایل).</li>
            <li>۲. پیام خوش‌آمد و گوینده را انتخاب کنید.</li>
            <li>۳. تلفن هوشمند را بسازید تا داخلی اختصاصی دریافت کنید.</li>
          </ol>
        </Card>
        <Card className="animate-in">
          <h3 className="font-bold text-slate-800">حساب کاربری</h3>
          <dl className="mt-3 space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-slate-500">موبایل</dt>
              <dd className="font-medium text-slate-800">{toFa(me?.phoneNumber ?? '')}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-slate-500">برند</dt>
              <dd className="font-medium text-slate-800">{me?.brandName}</dd>
            </div>
          </dl>
        </Card>
      </div>
    </div>
  )
}
