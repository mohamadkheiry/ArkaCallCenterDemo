import { useState } from 'react'
import { cn } from '../../components/ui'
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
  { key: 'openai', label: 'OpenAI و RAG' },
  { key: 'sms', label: 'SMS.ir' },
  { key: 'bale', label: 'کانال بله' },
  { key: 'reception', label: 'پذیرش و انتظار' },
  { key: 'branding', label: 'برندینگ' },
  { key: 'templates', label: 'پیامک‌ها و رویدادها' },
  { key: 'voices', label: 'گوینده‌ها' },
  { key: 'fallback', label: 'پیام پیش‌فرض' },
  { key: 'demos', label: 'دموها' },
  { key: 'calls', label: 'مکالمه‌ها' },
  { key: 'usage', label: 'مصرف توکن' },
  { key: 'users', label: 'کاربران' },
] as const

type TabKey = (typeof TABS)[number]['key']

export default function AdminPage() {
  const [tab, setTab] = useState<TabKey>('openai')

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">پنل سوپرادمین</h1>
        <p className="mt-1 text-sm text-slate-500">تنظیمات سراسری سامانه.</p>
      </div>

      <div className="flex flex-wrap gap-2">
        {TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={cn(
              'rounded-xl px-4 py-2 text-sm font-medium transition-colors',
              tab === t.key ? 'bg-brand-600 text-white shadow-md shadow-brand-600/25' : 'bg-white text-slate-600 hover:bg-slate-50',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'openai' && (
        <SettingsTab
          key="openai"
          groups={['openai', 'rag', 'limits']}
          title="OpenAI، RAG و محدودیت‌ها"
          desc="آدرس و کلید API اوپن‌ای‌آی، مدل‌ها، آستانه‌ی RAG و سقف پیش‌فرض مکالمه."
        />
      )}
      {tab === 'sms' && (
        <SettingsTab key="sms" groups={['sms']} title="تنظیمات SMS.ir" desc="کلید API و شماره خط سرویس پیامک." />
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
  )
}
