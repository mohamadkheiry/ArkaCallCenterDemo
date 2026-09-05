import { useEffect, useRef, useState } from 'react'
import { Pause, Play, TriangleAlert } from 'lucide-react'
import { api, apiError } from '../lib/api'

interface AudioPlayButtonProps {
  path: string
  showText?: boolean
}

interface PlaybackResources {
  audio: HTMLAudioElement | null
  url: string | null
  request: AbortController | null
}

function releaseMedia(resources: PlaybackResources) {
  const { audio, url } = resources
  resources.audio = null
  resources.url = null
  if (audio) {
    audio.onended = audio.onpause = audio.onplaying = audio.onerror = null
    audio.pause()
    audio.removeAttribute('src')
    audio.load()
  }
  if (url) URL.revokeObjectURL(url)
}

/**
 * فایل محافظت‌شده را با JWT به‌صورت Blob می‌گیرد و سپس پخش می‌کند. خطاهای HTTP،
 * فایل خالی/غیرصوتی و خطای خود مرورگر به‌جای سکوت کامل به کاربر نمایش داده می‌شوند.
 */
export default function AudioPlayButton(props: AudioPlayButtonProps) {
  // A different recording must never inherit the previous file, request or error.
  return <RecordingPlayer key={props.path} {...props} />
}

function RecordingPlayer({ path, showText = true }: AudioPlayButtonProps) {
  const [loading, setLoading] = useState(false)
  const [playing, setPlaying] = useState(false)
  const [error, setError] = useState('')
  const resourcesRef = useRef<PlaybackResources>({ audio: null, url: null, request: null })

  useEffect(() => {
    const resources = resourcesRef.current
    return () => {
      resources.request?.abort()
      resources.request = null
      releaseMedia(resources)
    }
    // Cleanup belongs to this player's lifetime, not the first Blob URL update:
    // pausing on a URL state change interrupts the first play() with AbortError.
  }, [])

  async function toggle() {
    const resources = resourcesRef.current
    if (resources.request) return
    setError('')
    if (resources.audio && !resources.audio.paused) {
      resources.audio.pause()
      return
    }

    // Lock synchronously, including decoding/play(), before React re-renders.
    const request = new AbortController()
    resources.request = request
    setLoading(true)
    try {
      let playableUrl = resources.url
      if (!playableUrl) {
        const response = await api.get<Blob>(path, { responseType: 'blob', signal: request.signal })
        if (request.signal.aborted) return
        const blob = response.data
        if (!(blob instanceof Blob) || blob.size <= 44 || !blob.type.toLowerCase().startsWith('audio/'))
          throw new Error('invalid-audio-response')
        playableUrl = URL.createObjectURL(blob)
        resources.url = playableUrl
      }

      const audio = resources.audio ?? new Audio()
      resources.audio = audio
      audio.preload = 'auto'
      if (audio.src !== playableUrl) audio.src = playableUrl
      audio.onended = () => setPlaying(false)
      audio.onpause = () => setPlaying(false)
      audio.onplaying = () => setPlaying(true)
      audio.onerror = () => {
        // Do not cache a corrupt/undecodable file across a user-initiated retry.
        releaseMedia(resources)
        setPlaying(false)
        setError('مرورگر نتوانست فایل صوتی را پخش کند.')
      }
      await audio.play()
    } catch (err) {
      if (request.signal.aborted) return
      setPlaying(false)
      setError(apiError(err, 'پخش فایل صوتی ممکن نشد؛ فایل را دوباره بررسی کنید.'))
    } finally {
      if (resources.request === request) resources.request = null
      if (!request.signal.aborted) setLoading(false)
    }
  }

  return (
    <div className="flex flex-col items-start gap-1">
      <button
        type="button"
        onClick={toggle}
        disabled={loading}
        aria-busy={loading}
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
        {showText && (loading ? 'بارگذاری…' : playing ? 'توقف' : 'پخش')}
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
