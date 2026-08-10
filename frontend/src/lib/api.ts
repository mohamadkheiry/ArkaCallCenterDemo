import axios from 'axios'

export const TOKEN_KEY = 'arka_token'
// هنگام ورود سوپرادمین به پنل یک کاربر (impersonation)، توکن اصلیِ سوپرادمین اینجا نگه داشته می‌شود.
export const ADMIN_TOKEN_KEY = 'arka_admin_token'
export const IMPERSONATING_KEY = 'arka_impersonating'

export const api = axios.create({
  baseURL: '/',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err?.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY)
      if (!location.pathname.startsWith('/login')) location.href = '/login'
    }
    return Promise.reject(err)
  },
)

/** استخراج پیام خطای فارسی از پاسخ سرور. */
export function apiError(err: unknown, fallback = 'خطایی رخ داد. دوباره تلاش کنید.'): string {
  if (axios.isAxiosError(err)) {
    return (err.response?.data as { error?: string })?.error ?? fallback
  }
  return fallback
}

/** زمان باقی‌مانده‌ی cooldown را از بدنه یا هدر استاندارد Retry-After می‌خواند. */
export function apiRetryAfter(err: unknown): number {
  if (!axios.isAxiosError(err)) return 0
  const bodyValue = Number((err.response?.data as { retryAfterSeconds?: number })?.retryAfterSeconds)
  if (Number.isFinite(bodyValue) && bodyValue > 0) return Math.ceil(bodyValue)
  const headerValue = Number(err.response?.headers?.['retry-after'])
  return Number.isFinite(headerValue) && headerValue > 0 ? Math.ceil(headerValue) : 0
}
