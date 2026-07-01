// Shared "how soon is this due" logic for reminder/note badges.

/** overdue -> red, within SOON_DAYS -> yellow, else (or no date) -> green. */
export type Urgency = 'green' | 'yellow' | 'red'

const SOON_DAYS = 30

/** Most urgent state across a set of due dates (nulls ignored; none -> green). */
export function dueUrgency(dates: (string | null)[]): Urgency {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  let soonest: number | null = null
  for (const d of dates) {
    if (!d) continue
    const days = Math.floor((new Date(`${d}T00:00:00`).getTime() - today.getTime()) / 86_400_000)
    if (soonest === null || days < soonest) soonest = days
  }
  if (soonest === null) return 'green'
  if (soonest < 0) return 'red'
  if (soonest <= SOON_DAYS) return 'yellow'
  return 'green'
}

/** Tinted pill (background + text) — used for the header notes badge. */
export const URGENCY_BADGE: Record<Urgency, string> = {
  green: 'bg-green-50 text-green-600',
  yellow: 'bg-amber-50 text-amber-700',
  red: 'bg-red-50 text-red-600',
}

/** Icon+count colour only — used for markers that sit inside another tag. */
export const URGENCY_TEXT: Record<Urgency, string> = {
  green: 'text-green-500',
  yellow: 'text-amber-500',
  red: 'text-red-600',
}
