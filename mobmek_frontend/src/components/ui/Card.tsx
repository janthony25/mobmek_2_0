import type { ReactNode } from 'react'

interface CardProps {
  title?: ReactNode
  /** Optional element rendered on the right of the title row (e.g. an Add button). */
  action?: ReactNode
  className?: string
  bodyClassName?: string
  children: ReactNode
}

/** White rounded card with a subtle shadow, and an optional title/action header. */
export function Card({ title, action, className = '', bodyClassName = '', children }: CardProps) {
  return (
    <section className={`rounded-xl border border-slate-200 bg-white p-5 shadow-sm ${className}`}>
      {(title || action) && (
        <div className="mb-4 flex items-center justify-between gap-3">
          {title && <h2 className="text-base font-semibold text-slate-900">{title}</h2>}
          {action}
        </div>
      )}
      <div className={bodyClassName}>{children}</div>
    </section>
  )
}
