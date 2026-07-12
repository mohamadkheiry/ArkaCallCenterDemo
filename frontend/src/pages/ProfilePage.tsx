import { useRef, useState } from 'react'
import { Camera, Trash2, Phone, ShieldCheck, UserRound } from 'lucide-react'
import { api, apiError } from '../lib/api'
import { toEn, toFa } from '../lib/format'
import { useAuth } from '../context/AuthContext'
import { Button, Card, TextInput, cn } from '../components/ui'

function Avatar({ userId, hasAvatar, version, size = 96 }: { userId: number; hasAvatar: boolean; version: number; size?: number }) {
  const { me } = useAuth()
  if (hasAvatar) {
    return (
      <img
        src={`/api/avatars/${userId}?v=${version}`}
        alt="تصویر پروفایل"
        className="rounded-2xl object-cover"
        style={{ width: size, height: size }}
      />
    )
  }
  return (
    <div
      className="grid place-items-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 font-bold text-white"
      style={{ width: size, height: size, fontSize: size * 0.4 }}
    >
      {me?.firstName?.[0] ?? '؟'}
    </div>
  )
}

export default function ProfilePage() {
  const { me, refresh } = useAuth()
  const [ver, setVer] = useState(Date.now())
  const [busy, setBusy] = useState(false)
  const [avatarMsg, setAvatarMsg] = useState('')
  const fileRef = useRef<HTMLInputElement>(null)

  // phone change
  const [step, setStep] = useState<'idle' | 'code'>('idle')
  const [newPhone, setNewPhone] = useState('')
  const [code, setCode] = useState('')
  const [phoneBusy, setPhoneBusy] = useState(false)
  const [phoneMsg, setPhoneMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  if (!me) return null

  async function uploadAvatar(file: File) {
    setAvatarMsg('')
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) return setAvatarMsg('فقط تصویر jpg/png/webp مجاز است.')
    if (file.size > 3 * 1024 * 1024) return setAvatarMsg('حجم تصویر حداکثر ۳ مگابایت.')
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', file)
      await api.post('/api/me/avatar', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      await refresh()
      setVer(Date.now())
      setAvatarMsg('تصویر پروفایل به‌روزرسانی شد.')
    } catch (e) {
      setAvatarMsg(apiError(e))
    } finally {
      setBusy(false)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  async function removeAvatar() {
    setBusy(true)
    try {
      await api.delete('/api/me/avatar')
      await refresh()
      setVer(Date.now())
      setAvatarMsg('تصویر حذف شد.')
    } finally {
      setBusy(false)
    }
  }

  async function requestCode() {
    setPhoneMsg(null)
    setPhoneBusy(true)
    try {
      await api.post('/api/me/phone/request-change', { newPhone: toEn(newPhone) })
      setStep('code')
      setPhoneMsg({ type: 'ok', text: 'کد تأیید به شماره‌ی جدید ارسال شد.' })
    } catch (e) {
      setPhoneMsg({ type: 'err', text: apiError(e) })
    } finally {
      setPhoneBusy(false)
    }
  }

  async function confirmCode() {
    setPhoneMsg(null)
    setPhoneBusy(true)
    try {
      await api.post('/api/me/phone/confirm-change', { newPhone: toEn(newPhone), code: toEn(code) })
      await refresh()
      setStep('idle')
      setNewPhone('')
      setCode('')
      setPhoneMsg({ type: 'ok', text: 'شماره با موفقیت تغییر کرد.' })
    } catch (e) {
      setPhoneMsg({ type: 'err', text: apiError(e) })
    } finally {
      setPhoneBusy(false)
    }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-slate-800">پروفایل</h1>
        <p className="mt-1 text-sm text-slate-500">تصویر پروفایل و شماره موبایل خود را مدیریت کنید.</p>
      </div>

      {/* تصویر پروفایل */}
      <Card className="animate-in">
        <div className="flex items-center gap-2">
          <UserRound size={19} className="text-brand-600" />
          <h3 className="text-lg font-bold text-slate-800">تصویر پروفایل</h3>
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-5">
          <Avatar userId={me.id} hasAvatar={me.hasAvatar} version={ver} />
          <div className="space-y-3">
            <div className="flex gap-2">
              <label className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:border-brand-300 hover:text-brand-700">
                <Camera size={16} />
                {busy ? 'در حال بارگذاری…' : me.hasAvatar ? 'تغییر تصویر' : 'بارگذاری تصویر'}
                <input
                  ref={fileRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  className="hidden"
                  disabled={busy}
                  onChange={(e) => e.target.files?.[0] && uploadAvatar(e.target.files[0])}
                />
              </label>
              {me.hasAvatar && (
                <Button variant="danger" onClick={removeAvatar} loading={busy} className="h-10 px-4 text-xs">
                  <Trash2 size={15} /> حذف
                </Button>
              )}
            </div>
            <p className="text-xs text-slate-400">jpg، png یا webp · حداکثر ۳ مگابایت</p>
            {avatarMsg && <p className="text-sm text-emerald-600">{avatarMsg}</p>}
          </div>
        </div>
      </Card>

      {/* شماره موبایل */}
      <Card className="animate-in">
        <div className="flex items-center gap-2">
          <Phone size={19} className="text-brand-600" />
          <h3 className="text-lg font-bold text-slate-800">شماره موبایل</h3>
        </div>
        <p className="mt-1 text-sm text-slate-500">
          شماره‌ی فعلی: <span className="font-semibold text-slate-800" dir="ltr">{toFa(me.phoneNumber)}</span>
        </p>

        <div className="mt-4 space-y-4">
          {step === 'idle' ? (
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex-1">
                <TextInput
                  label="شماره موبایل جدید"
                  inputMode="numeric"
                  dir="ltr"
                  placeholder="09xxxxxxxxx"
                  value={newPhone}
                  onChange={(e) => setNewPhone(e.target.value)}
                />
              </div>
              <Button onClick={requestCode} loading={phoneBusy} disabled={!newPhone.trim()}>
                ارسال کد تأیید
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="flex items-center gap-2 rounded-xl bg-brand-50 px-4 py-2.5 text-sm text-brand-700">
                <ShieldCheck size={16} />
                کد تأیید به شماره‌ی <span dir="ltr" className="font-semibold">{toFa(newPhone)}</span> ارسال شد.
              </div>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                <div className="flex-1">
                  <TextInput
                    label="کد ۶ رقمی"
                    inputMode="numeric"
                    dir="ltr"
                    maxLength={6}
                    className="text-center tracking-[0.4em]"
                    placeholder="------"
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                  />
                </div>
                <Button onClick={confirmCode} loading={phoneBusy} disabled={!code.trim()}>
                  تأیید و تغییر شماره
                </Button>
              </div>
              <button
                onClick={() => {
                  setStep('idle')
                  setCode('')
                  setPhoneMsg(null)
                }}
                className="text-sm text-slate-500 hover:text-brand-600"
              >
                انصراف / ویرایش شماره
              </button>
            </div>
          )}

          {phoneMsg && (
            <div
              className={cn(
                'rounded-xl px-4 py-3 text-sm',
                phoneMsg.type === 'ok' ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700',
              )}
            >
              {phoneMsg.text}
            </div>
          )}
        </div>
      </Card>
    </div>
  )
}
