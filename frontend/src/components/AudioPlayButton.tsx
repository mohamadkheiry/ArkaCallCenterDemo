import { useEffect, useRef, useState } from 'react'
import { Pause, Play, TriangleAlert } from 'lucide-react'
import { api, apiError } from '../lib/api'

/**
 * فایل محافظت‌شده را با JWT به‌صورت Blob می‌گیرد و سپس پخش می‌کند. خطاهای HTTP،
 * فایل خالی/غیرصوتی و خطای خود مرورگر به‌جای سکوت کامل به کاربر نمایش داده می‌شوند.
 */
export default function AudioPlayButton({ path, showText = true }: { path: string; showText?: boolean }) {
  const [url, setUrl] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [playing, setPlaying] = useState(false)
  const [error, setError] = useState('')
  const audioRef = useRef<HTMLAudioElement | null>(null)

  useEffect(() => {
    return () => {
      audioRef.current?.pause()
      if (url) URL.revokeObjectURL(url)
    }
  }, [url])

  async function toggle() {
    if (loading) return
    setError('')
    if (playing) {
      audioRef.current?.pause()
      return
    }

    try {
      let playableUrl = url
      if (!playableUrl) {
        setLoading(true)
        const response = await api.get<Blob>(path, { responseType: 'blob' })
        const blob = response.data
        if (!(blob instanceof Blob) || blob.size <= 44 || !blob.type.toLowerCase().startsWith('audio/'))
          throw new Error('invalid-audio-response')
        playableUrl = URL.createObjectURL(blob)
        setUrl(playableUrl)
      }

      const audio = audioRef.current ?? new Audio()
      audioRef.current = audio
      audio.preload = 'auto'
      if (audio.src !== playableUrl) audio.src = playableUrl
      audio.onended = () => setPlaying(false)
      audio.onpause = () => setPlaying(false)
      audio.onplay = () => setPlaying(true)
      audio.onerror = () => {
        setPlaying(false)
        setError('مرورگر نتوانست فایل صوتی را پخش کند.')
      }
      await audio.play()
    } catch (err) {
      setPlaying(false)
      setError(apiError(err, 'پخش فایل صوتی ممکن نشد؛ فایل را دوباره بررسی کنید.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex flex-col items-start gap-1">
      <button
        type="button"
        onClick={toggle}
        disabled={loading}
        aria-label={playing ? 'توقف پخش صوت' : 'پخش صوت'}
        className="flex items-center gap-1.5 rounded-lg bg-brand-50 px-3 py-1.5 text-xs font-medium text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-60"
      >
        {loading ? (
          <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-brand-300 border-t-brand-600" />
        ) : playing ? (
          <Pause size={14} />
        ) : (
          <Play size={14} />
        )}
        {showText && (playing ? 'توقف' : 'پخش')}
      </button>
      {error && (
        <span className="flex max-w-52 items-start gap-1 text-[11px] leading-5 text-rose-600" role="alert">
          <TriangleAlert size={12} className="mt-1 shrink-0" />
          {error}
        </span>
      )}
    </div>
  )
}
