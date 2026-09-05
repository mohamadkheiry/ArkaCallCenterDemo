import {
  ArrowLeft,
  AudioLines,
  BookOpen,
  Check,
  CircleAlert,
  Clock3,
  MessageSquare,
  Pencil,
  PhoneCall,
  ShieldCheck,
  Smartphone,
} from 'lucide-react'
import { Button, Logo, cn } from '../components/ui'
import { toFa } from '../lib/format'
import { OtpCodeInput } from './login/OtpCodeInput'
import { useLoginFlow } from './login/useLoginFlow'
import '../login.css'

const FEATURES = [
  { icon: AudioLines, text: 'صدای طبیعی' },
  { icon: BookOpen, text: 'دانش اختصاصی' },
  { icon: PhoneCall, text: 'داخلی مستقل' },
]

function BrandStory() {
  return (
    <aside className="auth-story" aria-label="تلفن هوشمند آرکا">
      <picture className="auth-art" aria-hidden="true">
        <source
          media="(max-width: 899px)"
          srcSet="/login-art/telephone-mobile.webp"
        />
        <img
          src="/login-art/telephone-studio.webp"
          alt=""
          width="1122"
          height="1402"
          fetchPriority="low"
          decoding="async"
        />
      </picture>
      <div className="auth-story-brand">
        <Logo size={46} />
      </div>
      <div className="auth-story-copy">
        <h2>
          هر تماس،<span>شروع یک ارتباط بهتر.</span>
        </h2>
        <p>
          منشی هوشمند آرکا، پاسخ‌گوی تماس‌های کسب‌وکار شما بر اساس دانش خودتان.
        </p>
      </div>
      <ul className="auth-features">
        {FEATURES.map(({ icon: Icon, text }) => (
          <li key={text}>
            <Icon size={25} strokeWidth={1.6} aria-hidden="true" />
            <span>{text}</span>
          </li>
        ))}
      </ul>
    </aside>
  )
}

function LoginProgress({ otp }: { otp: boolean }) {
  return (
    <ol className="auth-progress" aria-label="مراحل ورود">
      <li
        className={cn('is-active', otp && 'is-complete')}
        aria-current={!otp ? 'step' : undefined}
      >
        <span className="auth-step-number">
          {otp ? <Check size={17} aria-hidden="true" /> : '۱'}
        </span>
        <span>شماره موبایل</span>
      </li>
      <li
        className={cn(otp && 'is-active')}
        aria-current={otp ? 'step' : undefined}
      >
        <span className="auth-step-number">۲</span>
        <span>کد تأیید</span>
      </li>
    </ol>
  )
}

export default function LoginPage() {
  const flow = useLoginFlow()
  const isOtp = flow.step === 'otp'
  const busy = flow.busy !== null
  const sendingDisabled = busy || flow.retryAfter > 0

  return (
    <div className={cn('auth-page', isOtp && 'auth-page-otp')}>
      <header className="auth-mobile-brand">
        <Logo size={40} />
      </header>
      <BrandStory />
      <main className="auth-main" id="login-main">
        <div className="auth-form-wrap">
          <LoginProgress otp={isOtp} />
          <section className="auth-form-section" aria-labelledby="login-title">
            <div className="auth-form-intro" key={flow.step}>
              <h1 id="login-title">
                {isOtp ? 'کد تأیید را وارد کنید' : 'به آرکا خوش آمدید'}
              </h1>
              <p>
                {isOtp
                  ? flow.delivery === 'sms'
                    ? 'کد ۶ رقمی ارسال‌شده با پیامک را وارد کنید.'
                    : 'کد ۶ رقمی خوانده‌شده در تماس را وارد کنید.'
                  : 'برای ورود یا ساخت حساب، شماره موبایل خود را وارد کنید.'}
              </p>
            </div>
            <form
              noValidate
              onSubmit={(event) => {
                event.preventDefault()
                if (isOtp) void flow.verifyCode()
                else void flow.requestCode('sms')
              }}
              aria-busy={busy || undefined}
            >
              {isOtp ? (
                <div className="auth-otp-fields">
                  <div className="auth-destination">
                    <span>
                      <Smartphone size={17} aria-hidden="true" />
                      <bdi>{toFa(flow.phone)}</bdi>
                    </span>
                    <button
                      type="button"
                      onClick={flow.editPhone}
                      disabled={busy}
                    >
                      <Pencil size={14} aria-hidden="true" />
                      ویرایش شماره
                    </button>
                  </div>
                  <OtpCodeInput
                    key={flow.challengeId}
                    value={flow.code}
                    onChange={flow.updateCode}
                    invalid={flow.error?.field === 'code'}
                    disabled={busy}
                  />
                  <span id="login-code-hint" className="auth-code-hint">
                    کد را وارد کنید یا در این قسمت بچسبانید.
                  </span>
                </div>
              ) : (
                <div
                  className={cn(
                    'auth-phone-field',
                    flow.error?.field === 'phone' && 'has-error',
                  )}
                >
                  <label htmlFor="login-phone">شماره موبایل</label>
                  <div className="auth-phone-control">
                    <Smartphone
                      className="auth-phone-icon"
                      size={21}
                      strokeWidth={1.65}
                      aria-hidden="true"
                    />
                    <input
                      id="login-phone"
                      type="tel"
                      dir="ltr"
                      inputMode="tel"
                      autoComplete="tel"
                      enterKeyHint="go"
                      placeholder="0912 345 6789"
                      value={flow.phone}
                      onChange={(event) => flow.updatePhone(event.target.value)}
                      disabled={busy}
                      aria-invalid={flow.error?.field === 'phone' || undefined}
                      aria-describedby={
                        flow.error?.field === 'phone'
                          ? 'login-error'
                          : undefined
                      }
                    />
                    {flow.isPhoneValid && (
                      <Check
                        className="auth-phone-check"
                        size={18}
                        aria-hidden="true"
                      />
                    )}
                  </div>
                </div>
              )}
              {flow.error && (
                <div
                  className="auth-notice auth-notice-error"
                  id="login-error"
                  role="alert"
                >
                  <CircleAlert size={18} aria-hidden="true" />
                  <span>{flow.error.message}</span>
                </div>
              )}
              <Button
                type="submit"
                className="auth-primary"
                loading={flow.busy === (isOtp ? 'verify' : 'sms')}
                disabled={isOtp ? busy : sendingDisabled}
              >
                <span>
                  {isOtp ? 'تأیید و ورود به داشبورد' : 'دریافت کد با پیامک'}
                </span>
                <ArrowLeft size={20} aria-hidden="true" />
              </Button>
              {flow.retryAfter > 0 && (
                <div
                  className="auth-cooldown"
                  role="timer"
                  aria-label={`ارسال مجدد کد پس از ${toFa(flow.retryAfter)} ثانیه`}
                >
                  <Clock3 size={16} aria-hidden="true" />
                  <span>
                    ارسال مجدد کد تا{' '}
                    <strong>{toFa(flow.retryAfter)} ثانیه</strong> دیگر
                  </span>
                </div>
              )}
              <div className="auth-divider">
                <span>{isOtp ? 'کد را دریافت نکردید؟' : 'یا'}</span>
              </div>
              {isOtp && (
                <Button
                  type="button"
                  variant="outline"
                  className="auth-secondary"
                  loading={flow.busy === 'sms'}
                  disabled={sendingDisabled}
                  onClick={() => void flow.requestCode('sms')}
                >
                  <span>ارسال مجدد پیامک</span>
                  <MessageSquare size={19} aria-hidden="true" />
                </Button>
              )}
              <Button
                type="button"
                variant="outline"
                className={cn(
                  'auth-secondary',
                  isOtp && 'auth-call-alternative',
                )}
                loading={flow.busy === 'call'}
                disabled={sendingDisabled}
                onClick={() => void flow.requestCode('call')}
              >
                <span>دریافت کد با تماس تلفنی</span>
                <PhoneCall size={19} aria-hidden="true" />
              </Button>
              {isOtp && flow.delivery === 'call' && (
                <div className="auth-notice auth-notice-success" role="status">
                  <PhoneCall size={18} aria-hidden="true" />
                  <span>
                    درخواست تماس ثبت شد. کد ورود در تماس، رقم‌به‌رقم خوانده
                    می‌شود.
                  </span>
                </div>
              )}
              <div className="auth-security">
                <ShieldCheck size={24} strokeWidth={1.5} aria-hidden="true" />
                <p>
                  ورود امن با کد یک‌بارمصرف<span>بدون نیاز به رمز عبور</span>
                </p>
              </div>
            </form>
          </section>
        </div>
        <footer className="auth-footer">© آرکا · مرکز تماس هوشمند</footer>
      </main>
    </div>
  )
}
