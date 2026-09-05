import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  ArrowLeft,
  AudioLines,
  BookOpenText,
  Building2,
  ChevronLeft,
  Clock3,
  History,
  Phone,
  PlayCircle,
  Settings2,
} from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import { api } from '../lib/api'
import { toFa } from '../lib/format'

const ACTIONS = [
  {
    to: '/knowledge-base',
    title: 'پایگاه دانش',
    description: 'اطلاعات پاسخ‌گویی کسب‌وکار',
    icon: BookOpenText,
  },
  {
    to: '/smartphone',
    title: 'تلفن هوشمند',
    description: 'پیام خوشامد و تنظیمات داخلی',
    icon: Phone,
  },
  {
    to: '/voice',
    title: 'صدای گوینده',
    description: 'انتخاب و شنیدن صدای دستیار',
    icon: AudioLines,
  },
  {
    to: '/calls',
    title: 'تماس‌ها',
    description: 'تاریخچه و پرسش‌های بی‌پاسخ',
    icon: History,
  },
]

export default function DashboardHome() {
  const { me } = useAuth()
  const [videoAvailable, setVideoAvailable] = useState(false)
  const phone = me?.smartPhone
  const active = phone?.status === 'Active' && phone.extension != null
  const limit = me?.callMinuteLimit ?? null
  const used = Math.max(0, me?.usedMinutes ?? 0)
  const unlimited = me?.role === 'SuperAdmin'
  const knownLimit = !unlimited && limit != null && limit > 0
  const percent = knownLimit
    ? Math.min(100, Math.round((used / limit) * 100))
    : 0
  useEffect(() => {
    let alive = true
    api
      .get('/api/tutorial-video/info')
      .then(({ data }) => {
        if (alive) setVideoAvailable(!!data.available)
      })
      .catch(() => {})
    return () => {
      alive = false
    }
  }, [])

  return (
    <div className="overview-page">
      <div className="page-heading">
        <div>
          <h1>نمای کلی مرکز تماس</h1>
          <p>
            سلام {me?.firstName || 'همراه عزیز'}، به فضای کار خود خوش آمدید.
          </p>
        </div>
        <Link className="ui-button ui-button-outline" to="/smartphone">
          <Settings2 size={17} />
          تنظیمات تلفن
        </Link>
      </div>
      <section className="phone-summary" aria-label="وضعیت تلفن هوشمند">
        <div className="phone-summary-copy">
          <h2>
            <span
              className={active ? 'status-dot active' : 'status-dot pending'}
            />
            {active
              ? 'تلفن هوشمند شما آماده پاسخ‌گویی است'
              : 'تلفن هوشمندتان را راه‌اندازی کنید'}
          </h2>
          <p>دانش و صدای کسب‌وکارتان را از همین‌جا مدیریت کنید.</p>
          <div className="summary-actions">
            <Link
              className="ui-button ui-button-primary"
              to={active ? '/calls' : '/setup'}
            >
              {active ? 'مشاهده تماس‌ها' : 'راه‌اندازی تلفن'}
              <ArrowLeft size={16} />
            </Link>
            <Link className="ui-button ui-button-outline" to="/knowledge-base">
              ویرایش پایگاه دانش
            </Link>
          </div>
        </div>
        <dl className="phone-summary-numbers">
          <div>
            <dt>داخلی</dt>
            <dd>{phone?.extension != null ? toFa(phone.extension) : '—'}</dd>
          </div>
          <div>
            <dt>شماره پذیرش</dt>
            <dd className="reception-number" dir="ltr">
              {toFa(me?.receptionNumber ?? '02191008288')}
            </dd>
          </div>
        </dl>
      </section>
      <div className="overview-stats">
        <div className="overview-stat">
          <div>
            <p>شماره داخلی</p>
            <strong>
              {phone?.extension != null ? toFa(phone.extension) : '—'}
            </strong>
            <small className={active ? 'stat-active' : ''}>
              {active
                ? 'فعال'
                : phone
                  ? 'در حال آماده‌سازی'
                  : 'هنوز ساخته نشده'}
            </small>
          </div>
          <span className="stat-icon green">
            <Phone size={25} />
          </span>
        </div>
        <div className="overview-stat">
          <div>
            <p>دقایق استفاده‌شده</p>
            <strong>{toFa(used)}</strong>
            <small>دقیقه مکالمه</small>
          </div>
          <span className="stat-icon">
            <Clock3 size={25} />
          </span>
        </div>
        <div className="overview-stat">
          <div>
            <p>صدای گوینده</p>
            <strong className="voice-value">
              {me?.voiceName || 'پیش‌فرض'}
            </strong>
            <Link to="/voice">
              قابل تغییر
              <ChevronLeft size={12} />
            </Link>
          </div>
          <span className="stat-icon">
            <AudioLines size={26} />
          </span>
        </div>
      </div>
      <div className="overview-panels">
        <section className="overview-panel">
          <h2>مدیریت مرکز تماس</h2>
          <div className="quick-action-list">
            {ACTIONS.map(({ to, title, description, icon: Icon }) => (
              <Link to={to} className="quick-action-row" key={to}>
                <span className="quick-action-icon">
                  <Icon size={21} strokeWidth={1.7} />
                </span>
                <span>
                  <strong>{title}</strong>
                  <small>{description}</small>
                </span>
                <ChevronLeft size={18} />
              </Link>
            ))}
          </div>
        </section>
        <section className="overview-panel">
          <h2>مصرف سرویس</h2>
          <div className="usage-content">
            <div className="usage-details">
              <div>
                <span>
                  <i />
                  استفاده‌شده
                </span>
                <strong>{toFa(used)} دقیقه</strong>
              </div>
              <div>
                <span>
                  <i />
                  کل سهمیه
                </span>
                <strong>
                  {unlimited
                    ? 'نامحدود'
                    : knownLimit
                      ? `${toFa(limit)} دقیقه`
                      : 'بر اساس تنظیمات حساب'}
                </strong>
              </div>
              <p>
                {knownLimit
                  ? `${toFa(Math.max(0, limit - used))} دقیقه باقی‌مانده از ${toFa(limit)} دقیقه`
                  : unlimited
                    ? 'دسترسی نامحدود مدیریتی'
                    : 'سهمیه در تنظیمات حساب مشخص می‌شود.'}
              </p>
            </div>
            <div
              className="usage-ring"
              role="img"
              aria-label={
                knownLimit
                  ? `${toFa(percent)} درصد سهمیه مصرف شده`
                  : unlimited
                    ? 'سهمیه نامحدود'
                    : 'سهمیه مشخص نشده'
              }
            >
              <svg viewBox="0 0 160 160" aria-hidden="true">
                <circle cx="80" cy="80" r="67" className="ring-track" />
                <circle
                  cx="80"
                  cy="80"
                  r="67"
                  className="ring-value"
                  strokeDasharray={421}
                  strokeDashoffset={421 * (1 - percent / 100)}
                />
              </svg>
              <div>
                <strong>
                  {unlimited ? '∞' : knownLimit ? `${toFa(percent)}٪` : '—'}
                </strong>
                <small>
                  {unlimited
                    ? 'نامحدود'
                    : knownLimit
                      ? 'مصرف سهمیه'
                      : 'سهمیه حساب'}
                </small>
              </div>
            </div>
          </div>
        </section>
      </div>
      <section className="business-strip">
        <span className="business-icon">
          <Building2 size={28} strokeWidth={1.6} />
        </span>
        <div>
          <h2>{me?.brandName || 'کسب‌وکار شما'}</h2>
          <p>
            {[me?.firstName, me?.lastName].filter(Boolean).join(' ')}
            <span className="business-separator">·</span>
            <bdi>{toFa(me?.phoneNumber ?? '')}</bdi>
          </p>
        </div>
        <Link to="/profile" className="ui-button ui-button-outline">
          ویرایش پروفایل
        </Link>
      </section>
      {videoAvailable && (
        <Link to="/setup" className="tutorial-link">
          <PlayCircle size={20} />
          آموزش راه‌اندازی و استفاده از مرکز تماس
          <ArrowLeft size={17} />
        </Link>
      )}
    </div>
  )
}
