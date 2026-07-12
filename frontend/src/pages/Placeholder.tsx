import { Card } from '../components/ui'

export default function Placeholder({ title, phase }: { title: string; phase: string }) {
  return (
    <div className="mx-auto max-w-3xl">
      <Card className="animate-in text-center">
        <div className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-2xl bg-brand-50 text-2xl">
          🚧
        </div>
        <h2 className="text-xl font-extrabold text-slate-800">{title}</h2>
        <p className="mt-2 text-sm text-slate-500">
          این بخش در {phase} پیاده‌سازی می‌شود.
        </p>
      </Card>
    </div>
  )
}
