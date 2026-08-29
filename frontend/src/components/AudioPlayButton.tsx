import { useCallback, useEffect, useRef, useState } from 'react'
import { Pause, Play, TriangleAlert } from 'lucide-react'
import { api, apiError } from '../lib/api'

let sharedAudioContext: AudioContext | null = null

function getAudioContext() {
  sharedAudioContext ??= new AudioContext()
  return sharedAudioContext
}

/**
 * فایل محافظت‌شده را با JWT به‌صورت ArrayBuffer می‌گیرد و سپس پخش می‌کند. خطاهای HTTP،
 * فایل خالی/غیرصوتی و خطای خود مرورگر به‌جای سکوت کامل به کاربر نمایش داده می‌شوند.
 */
export default function AudioPlayButton({ path, showText = true }: { path: string; showText?: boolean }) {
  const [loading, setLoading] = useState(false)
  const [playing, setPlaying] = useState(false)
  const [error, setError] = useState('')
  const buttonRef = useRef<HTMLButtonElement | null>(null)
  const bufferRef = useRef<AudioBuffer | null>(null)
  const sourceRef = useRef<AudioBufferSourceNode | null>(null)
  const loadPromiseRef = useRef<Promise<AudioBuffer> | null>(null)

  const loadDecoded = useCallback(async (context: AudioContext): Promise<AudioBuffer> => {
    if (bufferRef.current) return bufferRef.current
    if (loadPromiseRef.current) return loadPromiseRef.current

    const promise = (async () => {
      const response = await api.get<ArrayBuffer>(path, { responseType: 'arraybuffer' })
      const contentType = String(response.headers['content-type'] ?? '').toLowerCase()
      if (!(response.data instanceof ArrayBuffer) || response.data.byteLength <= 44 || !contentType.startsWith('audio/'))
        throw new Error('invalid-audio-response')
      const decoded = await context.decodeAudioData(response.data.slice(0))
      bufferRef.current = decoded
      return decoded
    })()
    loadPromiseRef.current = promise
    try { return await promise } finally {
      if (loadPromiseRef.current === promise) loadPromiseRef.current = null
    }
  }, [path])

  useEffect(() => {
    sourceRef.current?.stop()
    sourceRef.current = null
    bufferRef.current = null
    loadPromiseRef.current = null
    setPlaying(false)
    setError('')

    const button = buttonRef.current
    if (!button || typeof IntersectionObserver === 'undefined') return
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some((entry) => entry.isIntersecting)) return
      observer.disconnect()
      // Only audio controls close to the viewport are prefetched. This keeps the
      // first click immediate without downloading a long knowledge-base page.
      void loadDecoded(getAudioContext()).catch(() => undefined)
    }, { rootMargin: '200px' })
    observer.observe(button)
    return () => {
      observer.disconnect()
      sourceRef.current?.stop()
      sourceRef.current = null
    }
  }, [path, loadDecoded])

  async function toggle() {
    if (loading) return
    setError('')
    if (playing) {
      sourceRef.current?.stop()
      sourceRef.current = null
      setPlaying(false)
      return
    }

    try {
      // resume() must start inside the user's click. If it is postponed until after
      // the protected file has been downloaded, Chrome/Android may reject the
      // first play attempt as autoplay and only accept the second click.
      const context = getAudioContext()
      const resumePromise = context.state === 'suspended' ? context.resume() : Promise.resolve()

      if (!bufferRef.current) setLoading(true)
      const decoded = await loadDecoded(context)
      const source = context.createBufferSource()
      source.buffer = decoded
      source.connect(context.destination)
      source.onended = () => {
        if (sourceRef.current !== source) return
        sourceRef.current = null
        setPlaying(false)
      }
      sourceRef.current = source
      source.start()
      setPlaying(true)
      await resumePromise
    } catch (err) {
      console.error('Protected audio playback failed.', err)
      sourceRef.current = null
      setPlaying(false)
      setError(apiError(err, 'پخش فایل صوتی ممکن نشد؛ فایل را دوباره بررسی کنید.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="flex flex-col items-start gap-1">
      <button
        ref={buttonRef}
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
