import { useState } from 'react'
import { cn } from '../../components/ui'
import { toFa } from '../../lib/format'

export function OtpCodeInput({
  value,
  onChange,
  invalid,
  disabled,
}: {
  value: string
  onChange: (value: string) => void
  invalid: boolean
  disabled: boolean
}) {
  const [focused, setFocused] = useState(false)
  return (
    <div className={cn('auth-code', invalid && 'auth-code-invalid')} dir="ltr">
      <input
        id="login-code"
        aria-label="کد تأیید ۶ رقمی"
        aria-describedby={
          invalid ? 'login-error login-code-hint' : 'login-code-hint'
        }
        aria-invalid={invalid || undefined}
        autoComplete="one-time-code"
        inputMode="numeric"
        enterKeyHint="go"
        autoFocus
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
      />
      <div className="auth-code-cells" aria-hidden="true">
        {Array.from({ length: 6 }, (_, index) => (
          <span
            key={index}
            className={cn(
              'auth-code-cell',
              value[index] && 'is-filled',
              focused && index === Math.min(value.length, 5) && 'is-focused',
            )}
          >
            {value[index] ? (
              toFa(value[index])
            ) : focused && index === value.length ? (
              <i />
            ) : (
              <b>—</b>
            )}
          </span>
        ))}
      </div>
    </div>
  )
}
