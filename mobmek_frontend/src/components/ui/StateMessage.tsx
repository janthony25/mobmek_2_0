interface StateMessageProps {
  title: string
  description?: string
}

/** Centered placeholder used for loading / empty / error states. */
export function StateMessage({ title, description }: StateMessageProps) {
  return (
    <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-slate-200 bg-white px-6 py-12 text-center">
      <p className="text-sm font-medium text-slate-700">{title}</p>
      {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
    </div>
  )
}
