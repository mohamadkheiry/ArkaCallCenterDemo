import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, TOKEN_KEY } from '../lib/api'
import type { Me } from '../types'

interface AuthState {
  me: Me | null
  loading: boolean
  isAuthenticated: boolean
  setToken: (token: string) => void
  refresh: () => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthState | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)

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
    setMe(null)
  }

  return (
    <AuthContext.Provider
      value={{ me, loading, isAuthenticated: !!me, setToken, refresh, logout }}
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
