import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, apiError } from '../lib/api'
import { toEn, toFa } from '../lib/format'
import { useAuth } from '../context/AuthContext'
import { Button, Card, Logo, TextInput } from '../components/ui'

export default function LoginPage() {
  const [step, setStep] = useState<'phone' | 'otp'>('phone')
  const [phone, setPhone] = useState('')
  const [code, setCode] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const { setToken } = useAuth()
  const navigate = useNavigate()

  async function requestOtp(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await api.post('/api/auth/request-otp', { phoneNumber: toEn(phone) })
      setStep('otp')
    } catch (err) {
      setError(apiError(err))
    } finally {
      setLoading(false)
    }
  }

  async function verifyOtp(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const { data } = await api.post('/api/auth/verify-otp', {
        phoneNumber: toEn(phone),
        code: toEn(code),
      })
      setToken(data.token)
      navigate(data.profileCompleted ? '/' : '/onboarding', { replace: true })
    } catch (err) {
      setError(apiError(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* پنل تبلیغاتی سمت راست */}
      <div className="relative hidden overflow-hidden bg-gradient-to-br from-brand-700 via-brand-600 to-indigo-800 lg:block">
        <div className="absolute inset-0 opacity-20 [background:radial-gradient(circle_at_30%_20%,white,transparent_40%),radial-gradient(circle_at_80%_70%,white,transparent_35%)]" />
        <div className="relative flex h-full flex-col justify-between p-12 text-white">
          <Logo size={48} />
          <div className="space-y-6">
            <h1 className="text-4xl font-extrabold leading-snug">
              منشی هوشمند شما،
              <br />
              همیشه پاسخگو
            </h1>
            <p className="max-w-md text-lg text-white/80">
              با هوش مصنوعی، تماس‌های کسب‌وکارتان را بر اساس پایگاه دانش اختصاصی خودتان
              پاسخ دهید — بی‌وقفه، طبیعی و حرفه‌ای.
            </p>
            <ul className="space-y-3 text-white/85">
              {['پاسخ‌گویی صوتی با هوش مصنوعی', 'پایگاه دانش اختصاصی (RAG)', 'داخلی اختصاصی روی سامانه'].map(
                (t) => (
                  <li key={t} className="flex items-center gap-3">
                    <span className="grid h-6 w-6 place-items-center rounded-full bg-white/20 text-xs">
                      ✓
                    </span>
                    {t}
                  </li>
                ),
              )}
            </ul>
          </div>
          <p className="text-sm text-white/50">© آرکا — سامانه تلفن هوشمند</p>
        </div>
      </div>

      {/* فرم ورود */}
      <div className="flex items-center justify-center p-6">
        <Card className="w-full max-w-md animate-in">
          <div className="mb-6 lg:hidden">
            <Logo />
          </div>
          <h2 className="text-2xl font-extrabold text-slate-800">
            {step === 'phone' ? 'ورود به داشبورد' : 'کد تأیید'}
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            {step === 'phone'
              ? 'شماره موبایل خود را وارد کنید تا کد ورود برایتان ارسال شود.'
              : `کد ارسال‌شده به شماره ${toFa(phone)} را وارد کنید.`}
          </p>

          {step === 'phone' ? (
            <form onSubmit={requestOtp} className="mt-6 space-y-4">
              <TextInput
                label="شماره موبایل"
                inputMode="numeric"
                dir="ltr"
                className="text-center tracking-widest"
                placeholder="09xxxxxxxxx"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                required
              />
              {error && <p className="text-sm text-rose-600">{error}</p>}
              <Button type="submit" loading={loading} className="w-full">
                دریافت کد ورود
              </Button>
            </form>
          ) : (
            <form onSubmit={verifyOtp} className="mt-6 space-y-4">
              <TextInput
                label="کد ۶ رقمی"
                inputMode="numeric"
                dir="ltr"
                maxLength={6}
                className="text-center text-lg tracking-[0.5em]"
                placeholder="------"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                required
              />
              {error && <p className="text-sm text-rose-600">{error}</p>}
              <Button type="submit" loading={loading} className="w-full">
                ورود
              </Button>
              <button
                type="button"
                onClick={() => {
                  setStep('phone')
                  setCode('')
                  setError('')
                }}
                className="w-full text-center text-sm text-slate-500 hover:text-brand-600"
              >
                ویرایش شماره موبایل
              </button>
            </form>
          )}
        </Card>
      </div>
    </div>
  )
}
