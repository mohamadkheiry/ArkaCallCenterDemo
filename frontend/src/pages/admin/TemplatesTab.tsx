import { useEffect, useState } from 'react'
import { api, apiError } from '../../lib/api'
import { Button, Card } from '../../components/ui'
import { SMS_EVENTS } from './adminData'

interface Template {
  eventType: string
  body: string
  enabled: boolean
}
interface Recipient {
  eventType: string
  useUserOwnNumber: boolean
  phoneNumber?: string | null
}

/** توضیح وضعیت مقصد پیامک بر اساس تنظیمات فعلی. */
function recipientHint(enabled: boolean, toUser: boolean, numbers?: string | null): string {
  if (!enabled) return 'این رویداد غیرفعال است؛ به هیچ‌کس پیامک ارسال نمی‌شود.'
  const hasNumbers = !!numbers && numbers.trim() !== ''
  if (toUser && hasNumbers) return 'ارسال به: خودِ کاربر + شماره‌های اضافی.'
  if (toUser) return 'ارسال به: فقط خودِ کاربر.'
  if (hasNumbers) return 'ارسال به: فقط شماره‌های تعیین‌شده.'
  return 'هیچ مقصدی انتخاب نشده؛ به هیچ‌کس ارسال نمی‌شود.'
}

export default function TemplatesTab() {
  const [templates, setTemplates] = useState<Record<string, Template>>({})
  const [recipients, setRecipients] = useState<Record<string, Recipient>>({})
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')

  useEffect(() => {
    async function load() {
      const [t, r] = await Promise.all([
        api.get<Template[]>('/api/admin/sms-templates'),
        api.get<Recipient[]>('/api/admin/sms-events'),
      ])
      const tm: Record<string, Template> = {}
      for (const e of SMS_EVENTS) tm[e.value] = { eventType: e.value, body: '', enabled: true }
      for (const x of t.data) tm[x.eventType] = x
      setTemplates(tm)

      const rm: Record<string, Recipient> = {}
      for (const e of SMS_EVENTS) rm[e.value] = { eventType: e.value, useUserOwnNumber: true, phoneNumber: '' }
      for (const x of r.data) rm[x.eventType] = { ...x, phoneNumber: x.phoneNumber ?? '' }
      setRecipients(rm)
    }
    load()
  }, [])

  async function save() {
    setBusy(true)
    setMsg('')
    try {
      await api.put('/api/admin/sms-templates', { templates: Object.values(templates) })
      await api.put('/api/admin/sms-events', { recipients: Object.values(recipients) })
      setMsg('پیامک‌ها و گیرندگان ذخیره شد.')
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="animate-in">
      <h3 className="text-lg font-bold text-slate-800">قالب پیامک‌ها و گیرندگان</h3>
      <p className="mt-1 text-sm text-slate-500">
        برای هر رویداد، متن پیامک و مقصد آن را تعیین کنید. می‌توانید همزمان به «خودِ کاربر» و «لیست
        شماره‌های ثابت» ارسال کنید، یا فقط یکی، یا با غیرفعال‌کردن رویداد به هیچ‌کس. متغیرها با
        {' {'}code{'}'}، {'{'}extension{'}'}، {'{'}firstName{'}'} پشتیبانی می‌شوند.
      </p>

      <div className="mt-5 space-y-4">
        {SMS_EVENTS.map((e) => {
          const t = templates[e.value]
          const r = recipients[e.value]
          if (!t || !r) return null
          return (
            <div key={e.value} className="rounded-2xl border border-slate-200 p-4">
              <div className="mb-3 flex items-center justify-between">
                <span className="text-sm font-bold text-slate-800">{e.label}</span>
                <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-500">
                  <input
                    type="checkbox"
                    checked={t.enabled}
                    onChange={(ev) => setTemplates((s) => ({ ...s, [e.value]: { ...t, enabled: ev.target.checked } }))}
                  />
                  فعال
                </label>
              </div>
              <textarea
                rows={2}
                value={t.body}
                onChange={(ev) => setTemplates((s) => ({ ...s, [e.value]: { ...t, body: ev.target.value } }))}
                className="w-full resize-none rounded-xl border border-slate-200 p-3 text-sm outline-none focus:border-brand-400 focus:ring-4 focus:ring-brand-100"
              />
              <div className="mt-3 space-y-2.5">
                <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
                  <input
                    type="checkbox"
                    checked={r.useUserOwnNumber}
                    onChange={(ev) => setRecipients((s) => ({ ...s, [e.value]: { ...r, useUserOwnNumber: ev.target.checked } }))}
                  />
                  ارسال به شماره‌ی خود کاربر
                </label>
                <div>
                  <input
                    dir="ltr"
                    placeholder="شماره‌های اضافی (با , جدا کنید) — خالی یعنی هیچ شماره‌ی ثابتی"
                    value={r.phoneNumber ?? ''}
                    onChange={(ev) => setRecipients((s) => ({ ...s, [e.value]: { ...r, phoneNumber: ev.target.value } }))}
                    className="h-10 w-full rounded-xl border border-slate-200 px-3 text-sm outline-none focus:border-brand-400"
                  />
                  <p className="mt-1.5 text-xs text-slate-400">{recipientHint(t.enabled, r.useUserOwnNumber, r.phoneNumber)}</p>
                </div>
              </div>
            </div>
          )
        })}
      </div>

      <div className="mt-5 flex items-center gap-4">
        <Button onClick={save} loading={busy}>
          ذخیره همه
        </Button>
        {msg && <span className="text-sm text-emerald-600">{msg}</span>}
      </div>
    </Card>
  )
}
