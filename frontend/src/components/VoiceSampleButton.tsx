import { useEffect, useState } from 'react'
import { Play, Square, Loader2 } from 'lucide-react'
import { cn } from './ui'

// پلیر سراسری: پخش نمونه‌ی جدید، نمونه‌ی قبلی را متوقف می‌کند.
let currentAudio: HTMLAudioElement | null = null
let currentStop: (() => void) | null = null

export default function VoiceSampleButton({
  voiceName,
  hasSample,
  className,
}: {
  voiceName: string
  hasSample: boolean
  className?: string
}) {
  const [state, setState] = useState<'idle' | 'loading' | 'playing'>('idle')

  useEffect(() => {
    return () => {
      // آنمانت: اگر این دکمه در حال پخش بود، متوقفش کن
      if (currentAudio && currentStop) currentStop()
    }
  }, [])

  function stop() {
    if (currentAudio) {
      currentAudio.pause()
      currentAudio.src = ''
      currentAudio = null
    }
    currentStop?.()
    currentStop = null
  }

  function toggle(e: React.MouseEvent) {
    e.stopPropagation()
    e.preventDefault()
    if (state === 'playing' || state === 'loading') {
      stop()
      setState('idle')
      return
    }
    stop() // نمونه‌ی دیگری اگر پخش است
    const audio = new Audio(`/api/voices/${voiceName}/sample`)
    currentAudio = audio
    currentStop = () => setState('idle')
    setState('loading')
    audio.oncanplay = () => setState('playing')
    audio.onended = () => {
      setState('idle')
      currentAudio = null
      currentStop = null
    }
    audio.onerror = () => {
      setState('idle')
      currentAudio = null
      currentStop = null
    }
    audio.play().catch(() => setState('idle'))
  }

  if (!hasSample) {
    return (
      <span
        className={cn(
          'grid h-9 w-9 place-items-center rounded-full bg-slate-100 text-slate-300',
          className,
        )}
        title="نمونه‌صدا موجود نیست"
      >
        <Play size={15} />
      </span>
    )
  }

  return (
    <button
      onClick={toggle}
      title={state === 'playing' ? 'توقف' : 'پخش نمونه‌صدا'}
      className={cn(
        'grid h-9 w-9 place-items-center rounded-full transition-all',
        state === 'playing'
          ? 'bg-brand-600 text-white shadow-md shadow-brand-600/30'
          : 'bg-brand-50 text-brand-600 hover:bg-brand-100',
        className,
      )}
    >
      {state === 'loading' ? (
        <Loader2 size={15} className="animate-spin" />
      ) : state === 'playing' ? (
        <Square size={13} />
      ) : (
        <Play size={15} />
      )}
    </button>
  )
}
