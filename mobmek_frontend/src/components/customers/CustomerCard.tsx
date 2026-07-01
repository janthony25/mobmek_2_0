import { Link } from 'react-router-dom'
import type { Car, Customer, Note, Reminder } from '@/types'

/** First letters of first + last name, e.g. "James Wilson" -> "JW". */
function initials(c: Customer): string {
  return `${c.firstName.charAt(0)}${c.lastName.charAt(0)}`.toUpperCase()
}

/** How close a due date is: overdue -> red, within SOON_DAYS -> yellow, else green. */
type Urgency = 'green' | 'yellow' | 'red'
const SOON_DAYS = 30

/** Most urgent state across a set of due dates (nulls ignored; none -> green). */
function dueUrgency(dates: (string | null)[]): Urgency {
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

/** Tinted pill (header notes badge). */
const URGENCY_BADGE: Record<Urgency, string> = {
  green: 'bg-green-50 text-green-600',
  yellow: 'bg-amber-50 text-amber-700',
  red: 'bg-red-50 text-red-600',
}

/** Icon+count colour only (per-car reminder marker, sits inside a slate tag). */
const URGENCY_TEXT: Record<Urgency, string> = {
  green: 'text-green-500',
  yellow: 'text-amber-500',
  red: 'text-red-600',
}

function PhoneIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-4 w-4 shrink-0" aria-hidden="true">
      <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.36 1.9.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.9.34 1.85.57 2.81.7A2 2 0 0 1 22 16.92Z" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function MailIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-4 w-4 shrink-0" aria-hidden="true">
      <rect x="2" y="4" width="20" height="16" rx="2" />
      <path d="m2 7 10 6 10-6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function CarIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-3.5 w-3.5 shrink-0" aria-hidden="true">
      <path d="M5 13l1.5-4.5A2 2 0 0 1 8.4 7h7.2a2 2 0 0 1 1.9 1.5L19 13" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M3 13h18v4a1 1 0 0 1-1 1h-1a1 1 0 0 1-1-1v-1H6v1a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-4Z" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="7.5" cy="15.5" r="0.5" fill="currentColor" />
      <circle cx="16.5" cy="15.5" r="0.5" fill="currentColor" />
    </svg>
  )
}

function NoteIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-3.5 w-3.5 shrink-0" aria-hidden="true">
      <path d="M8 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h9l5-5V5a2 2 0 0 0-2-2h-3" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M14 21v-4a1 1 0 0 1 1-1h4" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M9 3a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v1a1 1 0 0 1-1 1h-4a1 1 0 0 1-1-1V3Z" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function BellIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-3.5 w-3.5 shrink-0" aria-hidden="true">
      <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M13.73 21a2 2 0 0 1-3.46 0" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

interface CustomerCardProps {
  customer: Customer
  cars: Car[]
  /** Active (not-done) notes linked to this customer. */
  notes: Note[]
  /** Active (not-done) reminders for this customer, keyed to cars by carId. */
  reminders: Reminder[]
}

export function CustomerCard({ customer, cars, notes, reminders }: CustomerCardProps) {
  const noteUrgency = dueUrgency(notes.map((n) => n.dueDate))
  const since = new Date(customer.createdAtUtc).getFullYear()

  return (
    <Link
      to={`/customers/${customer.id}`}
      className="flex h-full flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:shadow-md"
    >
      {/* Header: avatar + name, with a vehicle-count badge on the right. */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-blue-100 text-base font-bold text-blue-600">
            {initials(customer)}
          </div>
          <div>
            <p className="font-semibold text-slate-900">
              {customer.firstName} {customer.lastName}
            </p>
            <p className="text-sm text-slate-500">Customer since {since}</p>
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-1.5">
          <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-600">
            <CarIcon />
            {cars.length}
          </span>
          {notes.length > 0 && (
            <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium ${URGENCY_BADGE[noteUrgency]}`}>
              <NoteIcon />
              {notes.length}
            </span>
          )}
        </div>
      </div>

      {/* Contact details */}
      <div className="space-y-1.5 text-sm text-slate-500">
        <div className="flex items-center gap-2">
          <PhoneIcon />
          <span>{customer.phoneNumber}</span>
        </div>
        {customer.emailAddress && (
          <div className="flex items-center gap-2">
            <MailIcon />
            <span className="truncate">{customer.emailAddress}</span>
          </div>
        )}
      </div>

      {/* Vehicle tags, each with an active-reminder marker coloured by how soon it's due. */}
      {cars.length > 0 && (
        <div className="mt-auto flex flex-wrap gap-2 pt-1">
          {cars.map((car) => {
            const carReminders = reminders.filter((r) => r.carId === car.id)
            const urgency = dueUrgency(carReminders.map((r) => r.dueDate))
            return (
              <span
                key={car.id}
                className="inline-flex items-center gap-1.5 rounded-md bg-slate-100 px-2.5 py-1 text-xs text-slate-700"
              >
                {car.year} {car.carMakeName} {car.carModelName}
                {carReminders.length > 0 && (
                  <span className={`inline-flex items-center gap-1 border-l border-slate-300 pl-1.5 font-medium ${URGENCY_TEXT[urgency]}`}>
                    <BellIcon />
                    {carReminders.length}
                  </span>
                )}
              </span>
            )
          })}
        </div>
      )}
    </Link>
  )
}
