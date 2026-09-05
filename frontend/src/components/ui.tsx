import { useEffect, useId, useState } from 'react'
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
} from 'react'

export function cn(...parts: (string | false | null | undefined)[]) {
  return parts.filter(Boolean).join(' ')
}

export function Button({
  children,
  className,
  variant = 'primary',
  loading,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'ghost' | 'outline' | 'danger'
  loading?: boolean
}) {
  const variants: Record<string, string> = {
    primary: 'ui-button-primary',
    ghost: 'ui-button-ghost',
    outline: 'ui-button-outline',
    danger: 'ui-button-danger',
  }
  return (
    <button
      className={cn('ui-button', variants[variant], className)}
      {...props}
      disabled={loading || props.disabled}
      aria-busy={loading || undefined}
    >
      {loading && (
        <span className="h-4 w-4 animate-spin rounded-full border-2 border-current/30 border-t-current" />
      )}
      {children}
    </button>
  )
}

export function TextInput({
  label,
  hint,
  className,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label?: string; hint?: string }) {
  const hintId = useId()
  return (
    <label className="block">
      {label && (
        <span className="mb-1.5 block text-sm font-medium text-slate-700">
          {label}
        </span>
      )}
      <input
        className={cn('ui-input', className)}
        {...props}
        aria-describedby={
          [props['aria-describedby'], hint ? hintId : undefined]
            .filter(Boolean)
            .join(' ') || undefined
        }
      />
      {hint && (
        <span
          id={hintId}
          className="mt-2 block text-xs leading-6 text-slate-500"
        >
          {hint}
        </span>
      )}
    </label>
  )
}

export function Card({
  children,
  className,
  hover = false,
}: {
  children: ReactNode
  className?: string
  hover?: boolean
}) {
  return (
    <div
      className={cn(
        'ui-card p-6',
        hover && 'hover:-translate-y-0.5 hover:shadow-soft-md',
        className,
      )}
    >
      {children}
    </div>
  )
}

export function Logo({ size = 40 }: { size?: number }) {
  const [hasLogo, setHasLogo] = useState<boolean | null>(null)
  useEffect(() => {
    let alive = true
    fetch('/api/branding/logo/info')
      .then((r) => (r.ok ? r.json() : { available: false }))
      .then((d) => alive && setHasLogo(!!d.available))
      .catch(() => alive && setHasLogo(false))
    return () => {
      alive = false
    }
  }, [])

  return (
    <div className="flex items-center gap-3">
      {hasLogo === null ? (
        <span
          className="skeleton rounded-2xl"
          style={{ width: size, height: size }}
        />
      ) : hasLogo ? (
        <img
          src="/api/branding/logo"
          alt="لوگو"
          className="rounded-2xl object-contain shadow-soft"
          style={{ width: size, height: size }}
          onError={() => setHasLogo(false)}
        />
      ) : (
        <div
          className="grid place-items-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-brand"
          style={{ width: size, height: size }}
        >
          <svg
            width={size * 0.55}
            height={size * 0.55}
            viewBox="0 0 24 24"
            fill="none"
          >
            <path
              d="M6.5 3h2.2c.5 0 .9.3 1 .8l.7 2.6c.1.4 0 .8-.3 1.1L8.4 9.1a12 12 0 0 0 6.5 6.5l1.6-1.7c.3-.3.7-.4 1.1-.3l2.6.7c.5.1.8.5.8 1v2.2c0 1-.8 1.8-1.8 1.7C11.6 19.8 4.2 12.4 4.8 4.8 4.8 3.8 5.6 3 6.5 3Z"
              fill="currentColor"
            />
          </svg>
        </div>
      )}
      <div className="leading-tight">
        <div className="text-base font-extrabold text-slate-800">آرکا</div>
        <div className="mt-1 text-xs text-slate-500">مرکز تماس هوشمند</div>
      </div>
    </div>
  )
}

export function Spinner() {
  return (
    <div className="grid min-h-[60vh] place-items-center">
      <span className="relative flex h-10 w-10">
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-brand-200 opacity-60" />
        <span className="relative inline-flex h-10 w-10 animate-spin rounded-full border-[3px] border-brand-100 border-t-brand-600" />
      </span>
    </div>
  )
}

/**
 * اسلایدرِ درصدی با پرشدگیِ رنگِ کنترل‌شده.
 *
 * چرا کامپوننتِ مشترک: در صفحه‌ی راست‌چین، پرشدگیِ پیش‌فرضِ مرورگر (accent-color) با
 * جهتِ حرکتِ دسته هماهنگ نمی‌شود و برچسبِ کمینه/بیشینه هم به‌سادگی برعکس می‌افتد.
 * اینجا جهت قطعی (چپ→راست: کمینه سمتِ چپ، بیشینه سمتِ راست) است، پرشدگی را خودمان
 * با گرادیان می‌کشیم، و برچسب‌ها در همان دستگاهِ مختصات چیده می‌شوند تا همیشه بخوانند.
 */
export function RangeSlider({
  value,
  min,
  max,
  step = 1,
  onChange,
  minLabel,
  maxLabel,
  disabled,
  className,
}: {
  value: number
  min: number
  max: number
  step?: number
  onChange: (v: number) => void
  /** برچسبِ سمتِ چپ (کمینه) */
  minLabel?: ReactNode
  /** برچسبِ سمتِ راست (بیشینه) */
  maxLabel?: ReactNode
  disabled?: boolean
  className?: string
}) {
  const pct = max > min ? ((value - min) / (max - min)) * 100 : 0
  const clamped = Math.min(100, Math.max(0, pct))
  return (
    <div className={className}>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        className="arka-range"
        style={{
          // پرشدگی دقیقاً تا محلِ دسته؛ بقیه‌ی ریل خاکستری.
          background: `linear-gradient(to right, var(--color-brand-600) 0%, var(--color-brand-600) ${clamped}%, #e2e8f0 ${clamped}%, #e2e8f0 100%)`,
        }}
      />
      {(minLabel || maxLabel) && (
        // dir=ltr تا برچسبِ کمینه واقعاً سمتِ چپ و بیشینه سمتِ راست بنشیند (هم‌راستا با خودِ اسلایدر).
        <div
          dir="ltr"
          className="mt-1.5 flex justify-between text-[11px] text-slate-400"
        >
          <span>{minLabel}</span>
          <span>{maxLabel}</span>
        </div>
      )}
    </div>
  )
}

/** بلوکِ اسکلتونِ بارگذاری. */
export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('skeleton', className)} />
}

/** چند خطِ اسکلتونِ متن. */
export function SkeletonText({
  lines = 3,
  className,
}: {
  lines?: number
  className?: string
}) {
  return (
    <div className={cn('space-y-2.5', className)}>
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton
          key={i}
          className={cn('h-3.5', i === lines - 1 ? 'w-2/3' : 'w-full')}
        />
      ))}
    </div>
  )
}

/** کارتِ اسکلتونِ کامل برای حالتِ بارگذاریِ صفحه. */
export function SkeletonCard({ lines = 3 }: { lines?: number }) {
  return (
    <div className="rounded-2xl border border-slate-200/60 bg-white/85 p-6 shadow-soft">
      <Skeleton className="mb-4 h-5 w-1/3" />
      <SkeletonText lines={lines} />
    </div>
  )
}
