import { useEffect, useRef, useState } from 'react'
import { ImageUp, Trash2, Image as ImageIcon } from 'lucide-react'
import { api, apiError } from '../../lib/api'
import { Button, Card } from '../../components/ui'

export default function BrandingTab() {
  const [hasLogo, setHasLogo] = useState(false)
  const [ver, setVer] = useState(Date.now())
  const [busy, setBusy] = useState(false)
  const [msg, setMsg] = useState('')
  const ref = useRef<HTMLInputElement>(null)

  function loadInfo() {
    api.get('/api/branding/logo/info').then(({ data }) => setHasLogo(!!data.available))
  }
  useEffect(loadInfo, [])

  async function upload(file: File) {
    setMsg('')
    if (!['image/png', 'image/jpeg', 'image/webp', 'image/svg+xml'].includes(file.type)) {
      return setMsg('فقط تصویر png/jpg/webp/svg مجاز است.')
    }
    setBusy(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/api/admin/logo', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      setMsg(data.message)
      setHasLogo(true)
      setVer(Date.now())
    } catch (e) {
      setMsg(apiError(e))
    } finally {
      setBusy(false)
      if (ref.current) ref.current.value = ''
    }
  }

  async function remove() {
    if (!confirm('لوگوی سامانه حذف شود؟')) return
    setBusy(true)
    try {
      await api.delete('/api/admin/logo')
      setHasLogo(false)
      setMsg('لوگو حذف شد.')
      setVer(Date.now())
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="animate-in">
      <div className="flex items-center gap-2">
        <ImageIcon size={19} className="text-brand-600" />
        <h3 className="text-lg font-bold text-slate-800">لوگوی سامانه</h3>
      </div>
      <p className="mt-1 text-sm text-slate-500">
        لوگویی که در سربرگ داشبورد و سایدبار نمایش داده می‌شود. png، jpg، webp یا svg.
      </p>

      <div className="mt-5 flex flex-wrap items-center gap-5">
        <div className="grid h-24 w-24 place-items-center rounded-2xl border border-slate-200 bg-slate-50 p-2">
          {hasLogo ? (
            <img src={`/api/branding/logo?v=${ver}`} alt="لوگو" className="max-h-full max-w-full object-contain" />
          ) : (
            <ImageIcon size={28} className="text-slate-300" />
          )}
        </div>
        <div className="space-y-3">
          <div className="flex gap-2">
            <label className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition-colors hover:border-brand-300 hover:text-brand-700">
              <ImageUp size={16} />
              {busy ? 'در حال بارگذاری…' : hasLogo ? 'تغییر لوگو' : 'بارگذاری لوگو'}
              <input
                ref={ref}
                type="file"
                accept="image/png,image/jpeg,image/webp,image/svg+xml"
                className="hidden"
                disabled={busy}
                onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])}
              />
            </label>
            {hasLogo && (
              <Button variant="danger" onClick={remove} loading={busy} className="h-10 px-4 text-xs">
                <Trash2 size={15} /> حذف
              </Button>
            )}
          </div>
          {msg && <p className="text-sm text-emerald-600">{msg}</p>}
        </div>
      </div>
    </Card>
  )
}
