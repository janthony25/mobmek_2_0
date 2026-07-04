import { Spinner } from './Spinner'

interface StateMessageProps {
  title: string
  description?: string
  /** Shows a spinner above the title — use for in-flight loading states. */
  loading?: boolean
}

/** Centered placeholder used for loading / empty / error states. */
export function StateMessage({ title, description, loading }: StateMessageProps) {
  return (
    <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-slate-200 bg-white px-6 py-12 text-center">
      {loading && <Spinner className="mb-2 h-5 w-5 text-slate-400" />}
      <p className="text-sm font-medium text-slate-700">{title}</p>
      {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
    </div>
  )
}
