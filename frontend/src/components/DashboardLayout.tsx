import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { Logo, cn } from './ui'

interface NavItem {
  to: string
  label: string
  icon: string
  end?: boolean
  adminOnly?: boolean
}

const NAV: NavItem[] = [
  { to: '/', label: 'داشبورد', icon: '🏠', end: true },
  { to: '/knowledge-base', label: 'پایگاه دانش', icon: '📚' },
  { to: '/voice', label: 'صدای گوینده', icon: '🎙️' },
  { to: '/calls', label: 'تماس‌ها', icon: '📞' },
  { to: '/admin', label: 'پنل سوپرادمین', icon: '🛡️', adminOnly: true },
]

export default function DashboardLayout() {
  const { me, logout } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const isAdmin = me?.role === 'SuperAdmin'

  const items = NAV.filter((n) => !n.adminOnly || isAdmin)

  return (
    <div className="flex min-h-screen">
      {/* سایدبار */}
      <aside
        className={cn(
          'fixed inset-y-0 right-0 z-40 w-72 transform border-l border-slate-200 bg-white/90 backdrop-blur-md transition-transform lg:static lg:translate-x-0',
          open ? 'translate-x-0' : 'translate-x-full lg:translate-x-0',
        )}
      >
        <div className="flex h-full flex-col p-5">
          <div className="px-2 py-2">
            <Logo />
          </div>
          <nav className="mt-6 flex-1 space-y-1">
            {items.map((n) => (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.end}
                onClick={() => setOpen(false)}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-brand-50 text-brand-700'
                      : 'text-slate-600 hover:bg-slate-50',
                  )
                }
              >
                <span className="text-lg">{n.icon}</span>
                {n.label}
              </NavLink>
            ))}
          </nav>
          <button
            onClick={() => {
              logout()
              navigate('/login', { replace: true })
            }}
            className="flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-medium text-rose-600 transition-colors hover:bg-rose-50"
          >
            <span className="text-lg">↩</span>
            خروج
          </button>
        </div>
      </aside>

      {open && (
        <div
          className="fixed inset-0 z-30 bg-slate-900/30 lg:hidden"
          onClick={() => setOpen(false)}
        />
      )}

      {/* محتوا */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-slate-200 bg-white/70 px-5 backdrop-blur-md">
          <button
            className="grid h-10 w-10 place-items-center rounded-xl border border-slate-200 lg:hidden"
            onClick={() => setOpen(true)}
          >
            ☰
          </button>
          <div className="hidden text-sm text-slate-500 lg:block">
            خوش آمدید، <span className="font-semibold text-slate-800">{me?.firstName} {me?.lastName}</span>
          </div>
          <div className="flex items-center gap-3">
            <div className="text-left">
              <div className="text-sm font-semibold text-slate-800">{me?.brandName}</div>
              <div className="text-xs text-slate-400">{me?.role === 'SuperAdmin' ? 'سوپرادمین' : 'کاربر'}</div>
            </div>
            <div className="grid h-10 w-10 place-items-center rounded-full bg-gradient-to-br from-brand-500 to-brand-700 text-sm font-bold text-white">
              {me?.firstName?.[0] ?? '؟'}
            </div>
          </div>
        </header>

        <main className="flex-1 p-5 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
