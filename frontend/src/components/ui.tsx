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
      'bg-brand-600 text-white hover:bg-brand-700 shadow-lg shadow-brand-600/25 disabled:opacity-60',
    ghost: 'bg-transparent text-slate-600 hover:bg-slate-100',
    outline: 'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
    danger: 'bg-rose-600 text-white hover:bg-rose-700',
  }
  return (
    <button
      className={cn(
        'inline-flex h-12 items-center justify-center gap-2 rounded-xl px-5 text-sm font-semibold transition-all active:scale-[.98] disabled:cursor-not-allowed',
        variants[variant],
        className,
      )}
      disabled={loading || props.disabled}
      {...props}
    >
      {loading && (
        <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white" />
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
          'h-12 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-800 outline-none transition-all placeholder:text-slate-400 focus:border-brand-400 focus:ring-4 focus:ring-brand-100',
          className,
        )}
        {...props}
      />
      {hint && <span className="mt-1 block text-xs text-slate-400">{hint}</span>}
    </label>
  )
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-slate-200/70 bg-white/80 p-6 shadow-sm backdrop-blur-sm',
        className,
      )}
    >
      {children}
    </div>
  )
}

export function Logo({ size = 40 }: { size?: number }) {
  return (
    <div className="flex items-center gap-3">
      <div
        className="grid place-items-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-lg shadow-brand-600/30"
        style={{ width: size, height: size }}
      >
        <svg width={size * 0.55} height={size * 0.55} viewBox="0 0 24 24" fill="none">
          <path
            d="M6.5 3h2.2c.5 0 .9.3 1 .8l.7 2.6c.1.4 0 .8-.3 1.1L8.4 9.1a12 12 0 0 0 6.5 6.5l1.6-1.7c.3-.3.7-.4 1.1-.3l2.6.7c.5.1.8.5.8 1v2.2c0 1-.8 1.8-1.8 1.7C11.6 19.8 4.2 12.4 4.8 4.8 4.8 3.8 5.6 3 6.5 3Z"
            fill="currentColor"
          />
        </svg>
      </div>
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
      <span className="h-8 w-8 animate-spin rounded-full border-[3px] border-brand-200 border-t-brand-600" />
    </div>
  )
}
