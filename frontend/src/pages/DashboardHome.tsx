import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Phone, Timer, Mic, Sparkles, PlayCircle, ListChecks, UserRound } from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import { api } from '../lib/api'
import { Button, Card } from '../components/ui'
import { toFa } from '../lib/format'

function StatCard({
  title,
  value,
  sub,
  icon: Icon,
}: {
  title: string
  value: string
  sub?: string
  icon: React.ComponentType<{ size?: number | string; className?: string }>
}) {
  return (
    <Card hover>
      <div className="flex items-start justify-between">
        <div>
          <div className="text-sm text-slate-500">{title}</div>
          <div className="mt-2 text-2xl font-extrabold text-slate-800">{value}</div>
          {sub && <div className="mt-1 text-xs text-slate-400">{sub}</div>}
        </div>
        <div className="grid h-12 w-12 place-items-center rounded-2xl bg-gradient-to-br from-brand-50 to-brand-100 text-brand-600 shadow-soft">
          <Icon size={21} />
        </div>
      </div>
    </Card>
  )
}

export default function DashboardHome() {
  const { me } = useAuth()
  const [videoAvailable, setVideoAvailable] = useState(false)
  const sp = me?.smartPhone
  const isActive = sp?.status === 'Active' && sp.extension != null
  const limit = me?.callMinuteLimit ?? null

  useEffect(() => {
    api.get('/api/tutorial-video/info').then(({ data }) => setVideoAvailable(!!data.available)).catch(() => {})
  }, [])

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">داشبورد</h1>
        <p className="mt-1 text-sm text-slate-500">وضعیت تلفن هوشمند و پایگاه دانش شما.</p>
      </div>

      {!isActive && (
        <Card className="animate-in border-brand-200 bg-gradient-to-l from-brand-50 to-white">
          <div className="flex flex-col items-start justify-between gap-4 sm:flex-row sm:items-center">
            <div className="flex items-start gap-3">
              <span className="mt-0.5 grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-100 text-brand-600">
                <Sparkles size={21} />
              </span>
              <div>
                <h3 className="text-lg font-bold text-slate-800">هنوز تلفن هوشمندی فعال ندارید</h3>
                <p className="mt-1 text-sm text-slate-500">
                  با ویزارد راه‌اندازی سریع، قدم‌به‌قدم تا ساخت تلفن هوشمند پیش بروید.
                </p>
              </div>
            </div>
            <Link to="/setup">
              <Button>
                <Sparkles size={16} />
                راه‌اندازی سریع
              </Button>
            </Link>
          </div>
        </Card>
      )}

      <div className="stagger grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard
          title="شماره داخلی"
          value={sp?.extension != null ? toFa(sp.extension) : '—'}
          sub={isActive ? 'فعال' : sp ? 'در حال آماده‌سازی' : 'ساخته نشده'}
          icon={Phone}
        />
        <StatCard
          title="دقایق مصرف‌شده"
          value={toFa(me?.usedMinutes ?? 0)}
          sub={me?.role === 'SuperAdmin' ? 'نامحدود' : limit ? `از ${toFa(limit)} دقیقه` : 'محدودیت پیش‌فرض'}
          icon={Timer}
        />
        <StatCard title="گوینده" value={me?.voiceName ?? 'پیش‌فرض'} sub="قابل تغییر در بخش صدا" icon={Mic} />
      </div>

      {isActive && (
        <Card className="animate-in border-emerald-200 bg-gradient-to-l from-emerald-50 to-white">
          <div className="flex items-start gap-3">
            <span className="mt-0.5 grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-emerald-100 text-emerald-600">
              <Phone size={21} />
            </span>
            <div>
              <h3 className="text-lg font-bold text-slate-800">چطور با تلفن هوشمند خود تماس بگیرید؟</h3>
              <p className="mt-1 text-sm leading-7 text-slate-600">
                ابتدا با شماره‌ی{' '}
                <span dir="ltr" className="font-bold text-emerald-700">{toFa(me?.receptionNumber ?? '02191008288')}</span>{' '}
                تماس بگیرید، سپس پس از پخش پیام پذیرش، شماره داخلی اختصاصی خود{' '}
                <span dir="ltr" className="font-bold text-emerald-700">{toFa(sp!.extension!)}</span>{' '}
                را شماره‌گیری کنید.
              </p>
            </div>
          </div>
        </Card>
      )}

      {videoAvailable && (
        <Card className="animate-in">
          <div className="mb-3 flex items-center gap-2">
            <PlayCircle size={19} className="text-brand-600" />
            <h3 className="font-bold text-slate-800">ویدیوی آموزشی</h3>
          </div>
          <video src="/api/tutorial-video" controls className="w-full rounded-xl" preload="metadata" />
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="animate-in">
          <div className="mb-3 flex items-center gap-2">
            <ListChecks size={19} className="text-brand-600" />
            <h3 className="font-bold text-slate-800">راهنمای سریع</h3>
          </div>
          <ol className="space-y-2 text-sm text-slate-600">
            <li>۱. پایگاه دانش خود را وارد کنید (متن یا فایل).</li>
            <li>۲. پیام خوش‌آمد و گوینده را انتخاب کنید.</li>
            <li>۳. تلفن هوشمند را بسازید تا داخلی اختصاصی دریافت کنید.</li>
          </ol>
        </Card>
        <Card className="animate-in">
          <div className="mb-3 flex items-center gap-2">
            <UserRound size={19} className="text-brand-600" />
            <h3 className="font-bold text-slate-800">حساب کاربری</h3>
          </div>
          <dl className="space-y-2 text-sm">
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
