import type { ReactNode } from 'react'

export const controlClass =
  'mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500'

interface FieldProps {
  label: string
  required?: boolean
  className?: string
  children: ReactNode
}

/** Label wrapper used by the bespoke (cascading) forms. */
export function Field({ label, required, className, children }: FieldProps) {
  return (
    <label className={`block text-sm font-medium text-slate-700 ${className ?? ''}`}>
      <span>
        {label}
        {required && <span className="text-red-500"> *</span>}
      </span>
      {children}
    </label>
  )
}
