import { useState } from 'react'

// پیام گذرا با تفکیکِ موفقیت/خطا؛ برای رنگِ درست (سبز=موفق، قرمز=خطا).
export type Flash = { ok: boolean; text: string } | null

export function useFlash() {
  const [flash, setFlash] = useState<Flash>(null)
  return {
    flash,
    ok: (text: string) => setFlash({ ok: true, text }),
    fail: (text: string) => setFlash({ ok: false, text }),
    clear: () => setFlash(null),
  }
}
