import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, TOKEN_KEY, ADMIN_TOKEN_KEY, IMPERSONATING_KEY } from '../lib/api'
import type { Me } from '../types'

interface AuthState {
  me: Me | null
  loading: boolean
  isAuthenticated: boolean
  setToken: (token: string) => void
  refresh: () => Promise<void>
  logout: () => void
  /** ورود سوپرادمین به پنل یک کاربر؛ توکن فعلی حفظ و توکن کاربر جایگزین می‌شود. */
  impersonate: (userToken: string, label: string) => void
  /** بازگشت از پنل کاربر به پنل سوپرادمین. */
  stopImpersonating: () => void
  /** نام کاربری که هم‌اکنون پنلش در حال مشاهده است (یا null). */
  impersonating: string | null
}

const AuthContext = createContext<AuthState | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)
  const [impersonating, setImpersonating] = useState<string | null>(
    () => localStorage.getItem(IMPERSONATING_KEY),
  )

  async function refresh() {
    const token = localStorage.getItem(TOKEN_KEY)
    if (!token) {
      setMe(null)
      setLoading(false)
      return
    }
    try {
      const { data } = await api.get<Me>('/api/me')
      setMe(data)
    } catch {
      setMe(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    refresh()
  }, [])

  function setToken(token: string) {
    localStorage.setItem(TOKEN_KEY, token)
    setLoading(true)
    refresh()
  }

  function logout() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(ADMIN_TOKEN_KEY)
    localStorage.removeItem(IMPERSONATING_KEY)
    setImpersonating(null)
    setMe(null)
  }

  function impersonate(userToken: string, label: string) {
    const adminToken = localStorage.getItem(TOKEN_KEY)
    if (adminToken) localStorage.setItem(ADMIN_TOKEN_KEY, adminToken)
    localStorage.setItem(IMPERSONATING_KEY, label)
    setImpersonating(label)
    setToken(userToken)
  }

  function stopImpersonating() {
    const adminToken = localStorage.getItem(ADMIN_TOKEN_KEY)
    localStorage.removeItem(ADMIN_TOKEN_KEY)
    localStorage.removeItem(IMPERSONATING_KEY)
    setImpersonating(null)
    if (adminToken) setToken(adminToken)
  }

  return (
    <AuthContext.Provider
      value={{ me, loading, isAuthenticated: !!me, setToken, refresh, logout, impersonate, stopImpersonating, impersonating }}
    >
      {children}
    </AuthContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
