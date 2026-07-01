import type { ReactNode } from 'react'

export type Tone = 'slate' | 'green' | 'amber' | 'orange' | 'blue' | 'red'

const TONES: Record<Tone, string> = {
  slate: 'bg-slate-100 text-slate-600',
  green: 'bg-green-50 text-green-700',
  amber: 'bg-amber-50 text-amber-700',
  orange: 'bg-orange-50 text-orange-700',
  blue: 'bg-blue-50 text-blue-700',
  red: 'bg-red-50 text-red-700',
}

/** Pill-shaped status label. */
export function Badge({ tone = 'slate', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-1 text-xs font-medium ${TONES[tone]}`}>
      {children}
    </span>
  )
}
