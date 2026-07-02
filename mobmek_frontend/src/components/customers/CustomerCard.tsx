import { Link } from 'react-router-dom'
import { dueUrgency, URGENCY_BADGE, URGENCY_TEXT } from '@/lib/dueDate'
import { BellIcon, CarIcon, MailIcon, NoteIcon, PhoneIcon } from '@/components/ui/icons'
import type { CustomerListItem } from '@/types'

/** First letters of first + last name, e.g. "James Wilson" -> "JW". */
function initials(c: CustomerListItem): string {
  return `${c.firstName.charAt(0)}${c.lastName.charAt(0)}`.toUpperCase()
}

interface CustomerCardProps {
  /** List item carrying the car/note/reminder aggregates the card displays. */
  customer: CustomerListItem
}

export function CustomerCard({ customer }: CustomerCardProps) {
  const noteUrgency = dueUrgency([customer.nextNoteDueDate])
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
            <CarIcon className="h-3.5 w-3.5" />
            {customer.cars.length}
          </span>
          {customer.activeNoteCount > 0 && (
            <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium ${URGENCY_BADGE[noteUrgency]}`}>
              <NoteIcon className="h-3.5 w-3.5" />
              {customer.activeNoteCount}
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
      {customer.cars.length > 0 && (
        <div className="mt-auto flex flex-wrap gap-2 pt-1">
          {customer.cars.map((car) => {
            const urgency = dueUrgency([car.nextReminderDueDate])
            return (
              <span
                key={car.id}
                className="inline-flex items-center gap-1.5 rounded-md bg-slate-100 px-2.5 py-1 text-xs text-slate-700"
              >
                {car.year} {car.carMakeName} {car.carModelName}
                {car.activeReminderCount > 0 && (
                  <span className={`inline-flex items-center gap-1 border-l border-slate-300 pl-1.5 font-medium ${URGENCY_TEXT[urgency]}`}>
                    <BellIcon className="h-3.5 w-3.5" />
                    {car.activeReminderCount}
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
