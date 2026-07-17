import { useEffect, useState } from 'react'
import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'

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
    primary:
      'bg-gradient-to-b from-brand-500 to-brand-600 text-white shadow-brand hover:from-brand-500 hover:to-brand-700 hover:-translate-y-0.5 disabled:opacity-60 disabled:translate-y-0',
    ghost: 'bg-transparent text-slate-600 hover:bg-slate-100',
    outline:
      'border border-slate-200 bg-white text-slate-700 shadow-soft hover:border-slate-300 hover:bg-slate-50 hover:-translate-y-0.5 disabled:translate-y-0',
    danger: 'bg-gradient-to-b from-rose-500 to-rose-600 text-white shadow-[0_6px_18px_rgba(225,29,72,0.28)] hover:-translate-y-0.5 disabled:translate-y-0',
  }
  return (
    <button
      className={cn(
        'inline-flex h-12 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold transition-all duration-200 ease-out active:scale-[.97] active:translate-y-0 disabled:cursor-not-allowed',
        variants[variant],
        className,
      )}
      disabled={loading || props.disabled}
      {...props}
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
  return (
    <label className="block">
      {label && <span className="mb-1.5 block text-sm font-medium text-slate-700">{label}</span>}
      <input
        className={cn(
          'h-12 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-800 shadow-soft outline-none transition-all duration-200 placeholder:text-slate-400 hover:border-slate-300 focus:border-brand-400 focus:ring-4 focus:ring-brand-100',
          className,
        )}
        {...props}
      />
      {hint && <span className="mt-1 block text-xs text-slate-400">{hint}</span>}
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
        'rounded-2xl border border-slate-200/60 bg-white/85 p-6 shadow-soft backdrop-blur-sm transition-all duration-300',
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
        <span className="skeleton rounded-2xl" style={{ width: size, height: size }} />
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
          <svg width={size * 0.55} height={size * 0.55} viewBox="0 0 24 24" fill="none">
            <path
              d="M6.5 3h2.2c.5 0 .9.3 1 .8l.7 2.6c.1.4 0 .8-.3 1.1L8.4 9.1a12 12 0 0 0 6.5 6.5l1.6-1.7c.3-.3.7-.4 1.1-.3l2.6.7c.5.1.8.5.8 1v2.2c0 1-.8 1.8-1.8 1.7C11.6 19.8 4.2 12.4 4.8 4.8 4.8 3.8 5.6 3 6.5 3Z"
              fill="currentColor"
            />
          </svg>
        </div>
      )}
      <div className="leading-tight">
        <div className="text-base font-extrabold text-slate-800">آرکا</div>
        <div className="text-[11px] text-slate-400">تلفن هوشمند</div>
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

/** بلوکِ اسکلتونِ بارگذاری. */
export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('skeleton', className)} />
}

/** چند خطِ اسکلتونِ متن. */
export function SkeletonText({ lines = 3, className }: { lines?: number; className?: string }) {
  return (
    <div className={cn('space-y-2.5', className)}>
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton key={i} className={cn('h-3.5', i === lines - 1 ? 'w-2/3' : 'w-full')} />
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
