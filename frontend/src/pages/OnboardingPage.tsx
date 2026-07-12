import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, apiError } from '../lib/api'
import { useAuth } from '../context/AuthContext'
import { Button, Card, Logo, TextInput } from '../components/ui'

export default function OnboardingPage() {
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [brandName, setBrandName] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const { refresh } = useAuth()
  const navigate = useNavigate()

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await api.post('/api/auth/profile', { firstName, lastName, brandName })
      await refresh()
      navigate('/', { replace: true })
    } catch (err) {
      setError(apiError(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-6">
      <Card className="w-full max-w-lg animate-in">
        <div className="mb-6 flex items-center justify-between">
          <Logo />
          <span className="rounded-full bg-brand-50 px-3 py-1 text-xs font-medium text-brand-700">
            مرحله ۱ از ۲
          </span>
        </div>
        <h2 className="text-2xl font-extrabold text-slate-800">تکمیل اطلاعات</h2>
        <p className="mt-1 text-sm text-slate-500">
          برای ساخت تلفن هوشمند، لطفاً اطلاعات زیر را کامل کنید.
        </p>

        <form onSubmit={submit} className="mt-6 grid gap-4 sm:grid-cols-2">
          <TextInput
            label="نام"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            required
          />
          <TextInput
            label="نام خانوادگی"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            required
          />
          <div className="sm:col-span-2">
            <TextInput
              label="نام برند / کسب‌وکار"
              value={brandName}
              onChange={(e) => setBrandName(e.target.value)}
              required
            />
          </div>
          {error && <p className="text-sm text-rose-600 sm:col-span-2">{error}</p>}
          <div className="sm:col-span-2">
            <Button type="submit" loading={loading} className="w-full">
              ادامه
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
