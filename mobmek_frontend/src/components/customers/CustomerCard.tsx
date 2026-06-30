import { Link } from 'react-router-dom'
import type { Car, Customer } from '@/types'

/** First letters of first + last name, e.g. "James Wilson" -> "JW". */
function initials(c: Customer): string {
  return `${c.firstName.charAt(0)}${c.lastName.charAt(0)}`.toUpperCase()
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

interface CustomerCardProps {
  customer: Customer
  cars: Car[]
}

export function CustomerCard({ customer, cars }: CustomerCardProps) {
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
        <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-600">
          <CarIcon />
          {cars.length}
        </span>
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

      {/* Vehicle tags */}
      {cars.length > 0 && (
        <div className="mt-auto flex flex-wrap gap-2 pt-1">
          {cars.map((car) => (
            <span key={car.id} className="rounded-md bg-slate-100 px-2.5 py-1 text-xs text-slate-700">
              {car.year} {car.carMakeName} {car.carModelName}
            </span>
          ))}
        </div>
      )}
    </Link>
  )
}
