import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { api, apiError, apiRetryAfter } from '../../lib/api'
import { toEn } from '../../lib/format'

type Delivery = 'sms' | 'call'
type BusyAction = Delivery | 'verify' | null
type LoginError = {
  message: string
  field: 'phone' | 'code' | 'request'
} | null

// Equivalent to AuthService.NormalizePhone, including pasted +98 numbers.
function normalizePhone(value: string) {
  let digits = toEn(value).replace(/\D/g, '')
  if (digits.startsWith('0098')) digits = `0${digits.slice(4)}`
  else if (digits.startsWith('98') && digits.length === 12)
    digits = `0${digits.slice(2)}`
  else if (digits.startsWith('9') && digits.length === 10) digits = `0${digits}`
  return digits
}

export function useLoginFlow() {
  const [step, setStep] = useState<'phone' | 'otp'>('phone')
  const [phone, setPhone] = useState('')
  const [code, setCode] = useState('')
  const [challengeId, setChallengeId] = useState(0)
  const [busy, setBusy] = useState<BusyAction>(null)
  const [error, setError] = useState<LoginError>(null)
  const [delivery, setDelivery] = useState<Delivery>('sms')
  const [cooldown, setCooldown] = useState({ phone: '', until: 0 })
  const [remaining, setRemaining] = useState(0)
  const inFlight = useRef(false)
  const { setToken } = useAuth()
  const navigate = useNavigate()
  const normalizedPhone = normalizePhone(phone)
  const isPhoneValid = /^09\d{9}$/.test(normalizedPhone)
  const retryAfter = normalizedPhone === cooldown.phone ? remaining : 0

  useEffect(() => {
    if (!cooldown.until) return
    // Absolute deadlines stay accurate when the browser resumes from the background.
    const tick = () => {
      const seconds = Math.max(
        0,
        Math.ceil((cooldown.until - Date.now()) / 1000),
      )
      setRemaining(seconds)
      if (seconds === 0) window.clearInterval(timer)
    }
    const timer = window.setInterval(tick, 500)
    document.addEventListener('visibilitychange', tick)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', tick)
    }
  }, [cooldown.until])

  function applyCooldown(seconds: number, targetPhone: string) {
    const duration = Number.isFinite(seconds)
      ? Math.max(0, Math.ceil(seconds))
      : 0
    setRemaining(duration)
    setCooldown({
      phone: targetPhone,
      until: duration ? Date.now() + duration * 1000 : 0,
    })
  }
  function updatePhone(value: string) {
    setPhone(value)
    if (error) setError(null)
  }
  function updateCode(value: string) {
    setCode(toEn(value).replace(/\D/g, '').slice(0, 6))
    if (error) setError(null)
  }

  async function requestCode(method: Delivery) {
    if (inFlight.current) return
    if (cooldown.phone === normalizedPhone && cooldown.until > Date.now())
      return
    if (!isPhoneValid) {
      setError({
        field: 'phone',
        message: 'شماره موبایل را درست وارد کنید؛ مانند ۰۹۱۲۳۴۵۶۷۸۹.',
      })
      return
    }
    const targetPhone = normalizedPhone
    inFlight.current = true
    setBusy(method)
    setError(null)
    try {
      const endpoint =
        method === 'call'
          ? '/api/auth/request-otp-call'
          : '/api/auth/request-otp'
      const { data } = await api.post(
        endpoint,
        { phoneNumber: targetPhone },
        { timeout: 60_000 },
      )
      applyCooldown(Number(data.retryAfterSeconds) || 0, targetPhone)
      setPhone(targetPhone)
      setDelivery(method)
      setCode('')
      setChallengeId((current) => current + 1)
      setStep('otp')
    } catch (err) {
      applyCooldown(apiRetryAfter(err), targetPhone)
      setError({
        field: 'request',
        message: apiError(
          err,
          'ارتباط با سرویس برقرار نشد. دوباره تلاش کنید یا کد را با تماس دریافت کنید.',
        ),
      })
    } finally {
      inFlight.current = false
      setBusy(null)
    }
  }
  async function verifyCode() {
    if (inFlight.current) return
    if (code.length !== 6) {
      setError({ field: 'code', message: 'کد تأیید ۶ رقمی را کامل وارد کنید.' })
      return
    }
    inFlight.current = true
    setBusy('verify')
    setError(null)
    try {
      const { data } = await api.post(
        '/api/auth/verify-otp',
        { phoneNumber: normalizedPhone, code },
        { timeout: 30_000 },
      )
      setToken(data.token)
      navigate(data.profileCompleted ? '/' : '/onboarding', { replace: true })
    } catch (err) {
      setError({ field: 'code', message: apiError(err) })
    } finally {
      inFlight.current = false
      setBusy(null)
    }
  }
  function editPhone() {
    if (inFlight.current) return
    setStep('phone')
    setCode('')
    setError(null)
  }
  return {
    step,
    phone,
    code,
    challengeId,
    busy,
    error,
    delivery,
    retryAfter,
    isPhoneValid,
    updatePhone,
    updateCode,
    requestCode,
    verifyCode,
    editPhone,
  }
}
