import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  LayoutDashboard,
  BookOpenText,
  Phone,
  Mic,
  History,
  ShieldCheck,
  LogOut,
  Menu,
  CircleHelp,
  Sparkles,
} from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import { Logo, cn } from './ui'
import Tour, { TOUR_DONE_KEY } from './Tour'

interface NavItem {
  to: string
  label: string
  icon: React.ComponentType<{ size?: number | string; className?: string }>
  tour: string
  end?: boolean
  adminOnly?: boolean
}

const NAV: NavItem[] = [
  { to: '/', label: 'داشبورد', icon: LayoutDashboard, tour: 'dashboard', end: true },
  { to: '/setup', label: 'راه‌اندازی سریع', icon: Sparkles, tour: 'setup' },
  { to: '/knowledge-base', label: 'پایگاه دانش', icon: BookOpenText, tour: 'kb' },
  { to: '/smartphone', label: 'تلفن هوشمند', icon: Phone, tour: 'smartphone' },
  { to: '/voice', label: 'صدای گوینده', icon: Mic, tour: 'voice' },
  { to: '/calls', label: 'تماس‌ها', icon: History, tour: 'calls' },
  { to: '/admin', label: 'پنل سوپرادمین', icon: ShieldCheck, tour: 'admin', adminOnly: true },
]

export default function DashboardLayout() {
  const { me, logout } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [tourOpen, setTourOpen] = useState(false)
  const isAdmin = me?.role === 'SuperAdmin'

  const items = NAV.filter((n) => !n.adminOnly || isAdmin)

  // اولین ورود: تور راهنما به‌صورت خودکار
  useEffect(() => {
    if (!localStorage.getItem(TOUR_DONE_KEY)) {
      const t = setTimeout(() => setTourOpen(true), 600)
      return () => clearTimeout(t)
    }
  }, [])

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
                data-tour={n.tour}
                onClick={() => setOpen(false)}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-xl px-4 py-3 text-sm font-medium transition-colors',
                    isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-600 hover:bg-slate-50',
                  )
                }
              >
                <n.icon size={19} className="shrink-0" />
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
            <LogOut size={19} className="shrink-0" />
            خروج
          </button>
        </div>
      </aside>

      {open && (
        <div className="fixed inset-0 z-30 bg-slate-900/30 lg:hidden" onClick={() => setOpen(false)} />
      )}

      {/* محتوا */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-slate-200 bg-white/70 px-5 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <button
              className="grid h-10 w-10 place-items-center rounded-xl border border-slate-200 text-slate-600 lg:hidden"
              onClick={() => setOpen(true)}
              aria-label="منو"
            >
              <Menu size={20} />
            </button>
            <div className="hidden text-sm text-slate-500 lg:block">
              خوش آمدید،{' '}
              <span className="font-semibold text-slate-800">
                {me?.firstName} {me?.lastName}
              </span>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={() => setTourOpen(true)}
              className="grid h-10 w-10 place-items-center rounded-xl border border-slate-200 text-slate-500 transition-colors hover:border-brand-300 hover:text-brand-600"
              title="راهنمای سامانه"
              data-tour="help"
            >
              <CircleHelp size={19} />
            </button>
            <div className="text-left">
              <div className="text-sm font-semibold text-slate-800">{me?.brandName}</div>
              <div className="text-xs text-slate-400">{isAdmin ? 'سوپرادمین' : 'کاربر'}</div>
            </div>
            <Link to="/profile" title="پروفایل" className="block h-10 w-10 overflow-hidden rounded-full ring-2 ring-transparent transition hover:ring-brand-200">
              {me?.hasAvatar ? (
                <img src={`/api/avatars/${me.id}?v=${me.hasAvatar}`} alt="پروفایل" className="h-full w-full object-cover" />
              ) : (
                <div className="grid h-full w-full place-items-center bg-gradient-to-br from-brand-500 to-brand-700 text-sm font-bold text-white">
                  {me?.firstName?.[0] ?? '؟'}
                </div>
              )}
            </Link>
          </div>
        </header>

        <main className="flex-1 p-5 lg:p-8">
          <Outlet />
        </main>
      </div>

      <Tour open={tourOpen} isAdmin={isAdmin} onClose={() => setTourOpen(false)} onSidebarChange={setOpen} />
    </div>
  )
}
