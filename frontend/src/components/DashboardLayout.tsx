import { useEffect, useRef, useState } from 'react'
import {
  Link,
  NavLink,
  Outlet,
  useLocation,
  useNavigate,
} from 'react-router-dom'
import {
  LayoutDashboard,
  BookOpenText,
  Phone,
  AudioLines,
  History,
  ShieldCheck,
  LogOut,
  Menu,
  CircleHelp,
  Rocket,
  UserCog,
  X,
  ChevronLeft,
} from 'lucide-react'
import { useAuth } from '../context/AuthContext'
import { api } from '../lib/api'
import { Logo, cn } from './ui'
import Tour from './Tour'

const NAV = [
  {
    to: '/',
    label: 'داشبورد',
    icon: LayoutDashboard,
    tour: 'dashboard',
    end: true,
  },
  { to: '/setup', label: 'راه‌اندازی سریع', icon: Rocket, tour: 'setup' },
  {
    to: '/knowledge-base',
    label: 'پایگاه دانش',
    icon: BookOpenText,
    tour: 'kb',
  },
  { to: '/smartphone', label: 'تلفن هوشمند', icon: Phone, tour: 'smartphone' },
  { to: '/voice', label: 'صدای گوینده', icon: AudioLines, tour: 'voice' },
  { to: '/calls', label: 'تماس‌ها', icon: History, tour: 'calls' },
  {
    to: '/admin',
    label: 'پنل سوپرادمین',
    icon: ShieldCheck,
    tour: 'admin',
    adminOnly: true,
  },
]

export default function DashboardLayout() {
  const { me, refresh, logout, impersonating, stopImpersonating } = useAuth()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const [tourOpen, setTourOpen] = useState(false)
  const menuRef = useRef<HTMLButtonElement>(null)
  const sidebarRef = useRef<HTMLElement>(null)
  const isAdmin = me?.role === 'SuperAdmin'
  const items = NAV.filter((n) => !n.adminOnly || isAdmin)
  const title = NAV.find((n) => n.to === pathname)?.label ?? 'پروفایل'
  const needsTour = !!me && !me.hasCompletedTour

  useEffect(() => {
    if (needsTour) {
      const timer = setTimeout(() => setTourOpen(true), 600)
      return () => clearTimeout(timer)
    }
  }, [me?.id, needsTour])

  useEffect(() => {
    if (!open || tourOpen) return
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    sidebarRef.current?.querySelector<HTMLButtonElement>('button')?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
        menuRef.current?.focus()
      }
      if (event.key === 'Tab') {
        const controls = sidebarRef.current?.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled])',
        )
        if (!controls?.length) return
        const first = controls[0],
          last = controls[controls.length - 1]
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault()
          last.focus()
        }
        if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault()
          first.focus()
        }
      }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.body.style.overflow = previous
      document.removeEventListener('keydown', onKey)
    }
  }, [open, tourOpen])

  useEffect(() => {
    const media = window.matchMedia('(min-width: 1024px)')
    const closeOnDesktop = () => {
      if (media.matches) setOpen(false)
    }
    media.addEventListener('change', closeOnDesktop)
    return () => media.removeEventListener('change', closeOnDesktop)
  }, [])

  async function closeTour() {
    setTourOpen(false)
    if (!me?.hasCompletedTour) {
      try {
        await api.post('/api/me/tour/complete')
        await refresh()
      } catch {
        /* Retry on next login. */
      }
    }
  }

  return (
    <div className="workspace-shell">
      <a className="skip-link" href="#workspace-content">
        رفتن به محتوای صفحه
      </a>
      <aside
        ref={sidebarRef}
        id="workspace-navigation"
        aria-label="منوی اصلی"
        className={cn('workspace-sidebar', open && 'is-open')}
      >
        <div className="sidebar-brand">
          <Logo size={42} />
          <button
            className="icon-button sidebar-close"
            aria-label="بستن منو"
            onClick={() => {
              setOpen(false)
              menuRef.current?.focus()
            }}
          >
            <X size={20} />
          </button>
        </div>
        <nav className="workspace-nav" aria-label="بخش‌های مرکز تماس">
          {items.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.end}
              data-tour={n.tour}
              onClick={() => setOpen(false)}
              className={({ isActive }) =>
                cn('workspace-nav-link', isActive && 'is-active')
              }
            >
              <n.icon size={20} strokeWidth={1.75} />
              <span>{n.label}</span>
              <ChevronLeft className="nav-chevron" size={15} />
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <button
            className="sidebar-help"
            onClick={() => {
              setOpen(false)
              setTourOpen(true)
            }}
            data-tour="help"
          >
            <CircleHelp size={24} />
            <span>
              <strong>راهنمای سامانه</strong>
              <small>آشنایی گام‌به‌گام با امکانات</small>
            </span>
            <ChevronLeft size={16} />
          </button>
          <button
            className="sidebar-logout"
            onClick={() => {
              logout()
              navigate('/login', { replace: true })
            }}
          >
            <LogOut size={19} />
            خروج از حساب
          </button>
        </div>
      </aside>
      {open && (
        <button
          className="sidebar-backdrop"
          aria-label="بستن منوی اصلی"
          onClick={() => setOpen(false)}
        />
      )}
      <div className="workspace-body">
        {impersonating && (
          <div className="impersonation-banner">
            <span>
              <UserCog size={18} />
              شما در حال مشاهده‌ی پنل کاربر «{impersonating}» هستید.
            </span>
            <button
              onClick={() => {
                stopImpersonating()
                navigate('/admin', { replace: true })
              }}
            >
              بازگشت به پنل سوپرادمین
            </button>
          </div>
        )}
        <header className="workspace-header">
          <div className="flex min-w-0 items-center gap-3">
            <button
              ref={menuRef}
              className="icon-button menu-toggle"
              aria-label="باز کردن منو"
              aria-expanded={open}
              aria-controls="workspace-navigation"
              onClick={() => setOpen(true)}
            >
              <Menu size={21} />
            </button>
            <div className="workspace-breadcrumb">
              <span>فضای کار</span>
              <ChevronLeft size={14} />
              <strong>{title}</strong>
            </div>
          </div>
          <div className="header-account">
            <button
              className="icon-button header-help"
              aria-label="راهنمای سامانه"
              onClick={() => setTourOpen(true)}
            >
              <CircleHelp size={20} />
            </button>
            <Link
              to="/profile"
              className="account-link"
              aria-label="مشاهده پروفایل"
            >
              <span className="account-copy">
                <strong>
                  {me?.brandName ||
                    `${me?.firstName ?? ''} ${me?.lastName ?? ''}`.trim() ||
                    'حساب کاربری'}
                </strong>
                <small>{isAdmin ? 'سوپرادمین' : 'کاربر سازمانی'}</small>
              </span>
              <span className="account-avatar">
                {me?.hasAvatar ? (
                  <img
                    src={`/api/avatars/${me.id}?v=${me.avatarVersion ?? 0}`}
                    alt=""
                  />
                ) : (
                  (me?.firstName?.[0] ?? 'آ')
                )}
              </span>
            </Link>
          </div>
        </header>
        <main
          id="workspace-content"
          className="workspace-content"
          tabIndex={-1}
        >
          <Outlet />
        </main>
        <footer className="workspace-footer">
          <span>آرکا · مرکز تماس هوشمند</span>
          <span>فضای اختصاصی {me?.brandName || 'کسب‌وکار شما'}</span>
        </footer>
      </div>
      <Tour
        open={tourOpen}
        isAdmin={isAdmin}
        onClose={closeTour}
        onSidebarChange={setOpen}
      />
    </div>
  )
}
