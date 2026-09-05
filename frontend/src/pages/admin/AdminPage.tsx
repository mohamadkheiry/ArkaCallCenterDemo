import { useSearchParams } from 'react-router-dom'
import {
  AudioLines,
  Blocks,
  Bot,
  Building2,
  ChartNoAxesCombined,
  FileText,
  History,
  MessageCircle,
  MessageSquare,
  PhoneCall,
  PlayCircle,
  Settings2,
  Users,
} from 'lucide-react'
import SettingsTab from './SettingsTab'
import BaleTab from './BaleTab'
import TemplatesTab from './TemplatesTab'
import VoicesTab from './VoicesTab'
import FallbackTab from './FallbackTab'
import UsersTab from './UsersTab'
import UsageTab from './UsageTab'
import DemosTab from './DemosTab'
import ReceptionTab from './ReceptionTab'
import BrandingTab from './BrandingTab'
import CallsAdminTab from './CallsAdminTab'

const TABS = [
  { key: 'demos', label: 'دموها', icon: PlayCircle },
  { key: 'users', label: 'کاربران', icon: Users },
  { key: 'calls', label: 'مکالمه‌ها', icon: History },
  { key: 'usage', label: 'مصرف توکن', icon: ChartNoAxesCombined },
  { key: 'voices', label: 'گوینده‌ها', icon: AudioLines },
  { key: 'reception', label: 'پذیرش و انتظار', icon: PhoneCall },
  { key: 'fallback', label: 'پیام پیش‌فرض', icon: MessageCircle },
  { key: 'branding', label: 'برندینگ', icon: Building2 },
  { key: 'openai', label: 'OpenAI و پاسخ دانشی', icon: Bot },
  { key: 'sms', label: 'SMS.ir', icon: MessageSquare },
  { key: 'crm', label: 'CRM فروش', icon: Blocks },
  { key: 'bale', label: 'کانال بله', icon: Settings2 },
  { key: 'templates', label: 'پیامک‌ها و رویدادها', icon: FileText },
] as const

type TabKey = (typeof TABS)[number]['key']

export default function AdminPage() {
  const [params, setParams] = useSearchParams()
  const selected = params.get('tab')
  const tab: TabKey = TABS.some((t) => t.key === selected)
    ? (selected as TabKey)
    : 'demos'

  return (
    <div className="admin-page space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">
          پنل سوپرادمین
        </h1>
        <p className="mt-1 text-sm text-slate-500">
          دموها، کاربران و تنظیمات مرکز تماس را مدیریت کنید.
        </p>
      </div>

      <div className="admin-workspace">
        <nav className="admin-navigation" aria-label="بخش‌های مدیریت">
          {TABS.map((t) => (
            <button
              key={t.key}
              onClick={() => setParams({ tab: t.key })}
              aria-current={tab === t.key ? 'true' : undefined}
            >
              <t.icon size={16} strokeWidth={1.75} />
              {t.label}
            </button>
          ))}
        </nav>
        <div className="admin-content" key={tab}>
          {tab === 'openai' && (
            <SettingsTab
              key="openai"
              groups={['openai', 'limits']}
              title="OpenAI، پاسخ مستقیم دانشی و محدودیت‌ها"
              desc="آدرس و کلید API، مدلی که کل پایگاه دانش را مستقیم بررسی می‌کند، مدل Realtime و سقف پیش‌فرض مکالمه."
            />
          )}
          {tab === 'sms' && (
            <SettingsTab
              key="sms"
              groups={['sms']}
              title="تنظیمات SMS.ir"
              desc="کلید API و شماره خط سرویس پیامک."
            />
          )}
          {tab === 'crm' && (
            <SettingsTab
              key="crm"
              groups={['crm']}
              title="اتصال CRM فروش"
              desc="ورود عملیاتی با نام کاربری و رمز، سپس ثبت لید با Bearer token. رمز در پاسخ API ماسک می‌شود."
            />
          )}
          {tab === 'bale' && <BaleTab />}
          {tab === 'reception' && <ReceptionTab />}
          {tab === 'branding' && <BrandingTab />}
          {tab === 'templates' && <TemplatesTab />}
          {tab === 'voices' && <VoicesTab />}
          {tab === 'fallback' && <FallbackTab />}
          {tab === 'demos' && <DemosTab />}
          {tab === 'calls' && <CallsAdminTab />}
          {tab === 'usage' && <UsageTab />}
          {tab === 'users' && <UsersTab />}
        </div>
      </div>
    </div>
  )
}
