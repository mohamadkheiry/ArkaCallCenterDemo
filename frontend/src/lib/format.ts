const FA_DIGITS = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹']

/** تبدیل ارقام لاتین به فارسی برای نمایش. */
export function toFa(input: string | number): string {
  return String(input).replace(/\d/g, (d) => FA_DIGITS[Number(d)])
}

/** تبدیل ارقام فارسی/عربی به لاتین برای ارسال به سرور. */
export function toEn(input: string): string {
  return input
    .replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
}

/** تاریخ و ساعت شمسی (تقویم فارسی). */
export function faDateTime(iso: string | Date): string {
  try {
    return new Date(iso).toLocaleString('fa-IR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return '—'
  }
}

/** فقط تاریخ شمسی. */
export function faDate(iso: string | Date): string {
  try {
    return new Date(iso).toLocaleDateString('fa-IR', { year: 'numeric', month: '2-digit', day: '2-digit' })
  } catch {
    return '—'
  }
}

/** مدت به قالب دقیقه:ثانیه با ارقام فارسی. */
export function faDuration(sec: number): string {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${toFa(m)}:${toFa(s.toString().padStart(2, '0'))}`
}
