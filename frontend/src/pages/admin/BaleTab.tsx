import { useState } from 'react'
import { Send, Megaphone } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { useFlash } from '../../lib/flash'
import { Button, Card } from '../../components/ui'
import SettingsTab from './SettingsTab'

/**
 * تبِ «کانال بله» در پنل سوپرادمین:
 * تنظیمِ توکنِ ربات و آی‌دیِ پابلیکِ کانال + امکانِ ارسالِ پیامِ آزمایشی برای اطمینان از اتصال.
 */
export default function BaleTab() {
  const [testing, setTesting] = useState(false)
  const { flash, ok, fail, clear } = useFlash()

  async function sendTest() {
    setTesting(true)
    clear()
    try {
      const { data } = await api.post('/api/admin/bale/test')
      ok(data.message ?? 'پیام آزمایشی ارسال شد.')
    } catch (e) {
      fail(apiError(e))
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className="space-y-6">
      <SettingsTab
        key="bale"
        groups={['bale']}
        title="کانال بله"
        desc="با تنظیمِ توکنِ ربات و آی‌دیِ کانال، هر کاربرِ جدیدِ دمو در کانال اعلام می‌شود."
      />

      <Card className="animate-in">
        <div className="flex items-center gap-2">
          <Megaphone size={19} className="text-brand-600" />
          <h3 className="font-bold text-slate-800">چه چیزی در کانال اعلام می‌شود؟</h3>
        </div>
        <p className="mt-2 text-sm leading-7 text-slate-500">
          برای هر کاربر <b className="text-slate-700">حداکثر سه پیام</b> ارسال می‌شود — هر مرحله فقط یک‌بار:
        </p>
        <ol className="mt-3 space-y-2 text-sm text-slate-600">
          <li>
            <span className="font-semibold text-slate-800">۱. ورود به دمو</span> — به‌محضِ واردکردنِ شماره‌ی موبایل.
          </li>
          <li>
            <span className="font-semibold text-slate-800">۲. ثبت نام</span> — نام و نام‌خانوادگی (و برند).
          </li>
          <li>
            <span className="font-semibold text-slate-800">۳. ساخت تلفن هوشمند</span> — به‌همراه شماره‌ی داخلیِ ساخته‌شده.
          </li>
        </ol>
        <p className="mt-3 text-xs text-slate-400">
          نکته: ربات باید <b>ادمینِ کانال</b> باشد وگرنه بله اجازه‌ی ارسال نمی‌دهد. اگر پیامی ارسال نشد،
          با دکمه‌ی زیر اتصال را بررسی کنید.
        </p>

        <div className="mt-5 flex items-center gap-4">
          <Button onClick={sendTest} loading={testing} variant="outline">
            <Send size={16} />
            ارسال پیام آزمایشی به کانال
          </Button>
          {flash && (
            <span className={`text-sm ${flash.ok ? 'text-emerald-600' : 'text-rose-600'}`}>{flash.text}</span>
          )}
        </div>
      </Card>
    </div>
  )
}
