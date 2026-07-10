import { dateTime } from '@/lib/format'

interface UpdatedByTagProps {
  updatedAtUtc: string | null
  updatedByName: string | null
  className?: string
}

/** Small "Last updated by X on <date>" tag for detail pages. Renders nothing until a record
 * has actually been updated at least once (a freshly created row has no UpdatedAtUtc yet). */
export function UpdatedByTag({ updatedAtUtc, updatedByName, className }: UpdatedByTagProps) {
  if (!updatedAtUtc) return null

  return (
    <span className={`text-xs text-slate-400 ${className ?? ''}`}>
      Last updated {updatedByName ? `by ${updatedByName} ` : ''}
      {dateTime(updatedAtUtc)}
    </span>
  )
}
