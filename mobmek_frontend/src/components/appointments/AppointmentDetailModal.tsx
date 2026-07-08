import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { updateAppointment, deleteAppointment, toAppointmentRequest } from '@/api/appointments'
import { createCustomer } from '@/api/customers'
import { createCar } from '@/api/cars'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { useToast } from '@/components/ui/toast'
import { AppointmentForm } from '@/components/forms/AppointmentForm'
import { CarForm } from '@/components/forms/CarForm'
import { Field, controlClass } from '@/components/forms/controls'
import { date, time, orDash } from '@/lib/format'
import { APPOINTMENT_STATUS_LABELS, AppointmentStatus } from '@/types'
import type { Appointment, CreateAppointmentRequest, CreateCarRequest } from '@/types'

interface AppointmentDetailModalProps {
  appointment: Appointment
  onClose: () => void
  /** Called with the fresh appointment after any mutation, so the calendar can refresh. */
  onChanged: (updated: Appointment) => void
  onDeleted: () => void
  /** Show the "View in calendar" button — only relevant when opened from outside the calendar itself (e.g. the Job page). */
  showViewInCalendar?: boolean
}

/**
 * Appointment details plus the convert-on-arrival flow: a soft-contact booking is
 * turned into real records step by step — create customer → add car → create job —
 * each step prefilled from what was taken over the phone.
 */
export function AppointmentDetailModal({
  appointment,
  onClose,
  onChanged,
  onDeleted,
  showViewInCalendar = false,
}: AppointmentDetailModalProps) {
  const toast = useToast()
  const navigate = useNavigate()

  const [editing, setEditing] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [convertStep, setConvertStep] = useState<'customer' | 'car' | null>(null)
  const [updatingStatus, setUpdatingStatus] = useState(false)

  const a = appointment
  const isPast = new Date(a.endUtc) < new Date()

  const saveEdit = async (values: CreateAppointmentRequest) => {
    const updated = await updateAppointment(a.id, values)
    toast.success('Appointment updated.')
    setEditing(false)
    onChanged(updated)
  }

  const handleStatusChange = async (status: AppointmentStatus) => {
    setUpdatingStatus(true)
    try {
      const updated = await updateAppointment(a.id, toAppointmentRequest(a, { status }))
      toast.success('Status updated.')
      onChanged(updated)
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setUpdatingStatus(false)
    }
  }

  const handleDelete = async () => {
    await deleteAppointment(a.id)
    toast.success('Appointment deleted.')
    onDeleted()
  }

  /**
   * Step 3: hand over to the New Job page, prefilled from the appointment.
   * The job is created there (with parts/labour/services); on save the page
   * links the job back to this appointment and marks it Arrived.
   */
  const handleCreateJob = () => {
    if (!a.customerId || !a.carId) return
    navigate('/jobs/new', {
      state: { customerId: a.customerId, carId: a.carId, appointment: a },
    })
  }

  const handleCarCreated = async (values: Record<string, unknown>) => {
    const car = await createCar({ customerId: a.customerId, ...values } as CreateCarRequest)
    const updated = await updateAppointment(a.id, toAppointmentRequest(a, { carId: car.id }))
    toast.success('Car added and linked.')
    setConvertStep(null)
    onChanged(updated)
  }

  if (editing) {
    return (
      <Modal open title="Edit appointment" onClose={() => setEditing(false)} maxWidth="max-w-2xl">
        <AppointmentForm initial={a} onSubmit={saveEdit} onCancel={() => setEditing(false)} />
      </Modal>
    )
  }

  if (convertStep === 'customer') {
    return (
      <Modal open title="Create customer from contact" onClose={() => setConvertStep(null)} maxWidth="max-w-lg">
        <QuickCustomerForm
          appointment={a}
          onDone={(updated) => {
            setConvertStep(null)
            onChanged(updated)
          }}
          onCancel={() => setConvertStep(null)}
        />
      </Modal>
    )
  }

  if (convertStep === 'car') {
    return (
      <Modal open title={`Add car for ${a.customerName ?? 'customer'}`} onClose={() => setConvertStep(null)} maxWidth="max-w-2xl">
        {a.vehicleDescription && (
          <p className="mb-4 rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-600">
            From the call: “{a.vehicleDescription}”
          </p>
        )}
        <CarForm initial={null} onSubmit={handleCarCreated} onCancel={() => setConvertStep(null)} />
      </Modal>
    )
  }

  return (
    <>
      <Modal open title={a.title} onClose={onClose} maxWidth="max-w-2xl">
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={a.status}
              disabled={updatingStatus}
              onChange={(e) => handleStatusChange(Number(e.target.value) as AppointmentStatus)}
              aria-label="Status"
              className={`${controlClass.replace('mt-1 w-full ', '')} py-1 text-sm font-medium disabled:opacity-60`}
            >
              {Object.entries(APPOINTMENT_STATUS_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
            <span className="text-sm text-slate-600">
              {date(a.startUtc)} · {time(a.startUtc)} – {time(a.endUtc)}
            </span>
            {isPast && a.status === AppointmentStatus.Scheduled && (
              <Badge tone="amber">Past due</Badge>
            )}
          </div>

          <dl className="grid grid-cols-1 gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
            {a.customerId ? (
              <div>
                <dt className="text-slate-500">Customer</dt>
                <dd>
                  <button
                    type="button"
                    className="font-medium text-slate-900 underline decoration-slate-300 hover:decoration-slate-500"
                    onClick={() => navigate(`/customers/${a.customerId}`)}
                  >
                    {a.customerName}
                  </button>
                </dd>
              </div>
            ) : (
              <div>
                <dt className="text-slate-500">Contact (not yet a customer)</dt>
                <dd className="font-medium text-slate-900">
                  {orDash(a.contactName)} · {orDash(a.contactPhone)}
                </dd>
              </div>
            )}

            <div>
              <dt className="text-slate-500">Vehicle</dt>
              <dd className="font-medium text-slate-900">
                {a.carId && a.customerId ? (
                  <button
                    type="button"
                    className="underline decoration-slate-300 hover:decoration-slate-500"
                    onClick={() => navigate(`/customers/${a.customerId}/cars/${a.carId}`)}
                  >
                    {a.carDescription ?? orDash(a.vehicleDescription)}
                  </button>
                ) : (
                  (a.carDescription ?? orDash(a.vehicleDescription))
                )}
              </dd>
            </div>

            <div>
              <dt className="text-slate-500">Mechanic</dt>
              <dd className="font-medium text-slate-900">{orDash(a.mechanicName)}</dd>
            </div>

            <div>
              <dt className="text-slate-500">Job</dt>
              <dd className="font-medium text-slate-900">
                {a.jobId ? (
                  <button
                    type="button"
                    className="underline decoration-slate-300 hover:decoration-slate-500"
                    onClick={() => navigate(`/jobs/${a.jobId}`)}
                  >
                    {a.jobTitle ?? 'Open job'}
                  </button>
                ) : (
                  '—'
                )}
              </dd>
            </div>

            {a.notes && (
              <div className="sm:col-span-2">
                <dt className="text-slate-500">Notes</dt>
                <dd className="whitespace-pre-wrap text-slate-900">{a.notes}</dd>
              </div>
            )}
          </dl>

          {/* Check-in: walk the soft booking into real records, one prefilled step at a time. */}
          {!a.jobId && (
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
              <p className="mb-2 text-xs font-medium tracking-wide text-slate-500 uppercase">Check-in</p>
              <div className="flex flex-wrap gap-2">
                {!a.customerId && (
                  <Button size="sm" onClick={() => setConvertStep('customer')}>
                    1. Create customer
                  </Button>
                )}
                {a.customerId && !a.carId && (
                  <Button size="sm" onClick={() => setConvertStep('car')}>
                    2. Add car
                  </Button>
                )}
                {a.customerId && a.carId && (
                  <Button size="sm" onClick={handleCreateJob}>
                    3. Create job
                  </Button>
                )}
                <p className="w-full text-xs text-slate-500">
                  {!a.customerId
                    ? 'When the car arrives: create the customer record from the phone contact, then add the car, then open a job.'
                    : !a.carId
                      ? 'Customer linked — add the car next, then create the job.'
                      : 'Ready — opens the New Job page prefilled; saving the job links it back here.'}
                </p>
              </div>
            </div>
          )}

          <div className="flex justify-between border-t border-slate-100 pt-4">
            <Button variant="danger" onClick={() => setConfirmingDelete(true)}>
              Delete
            </Button>
            <div className="flex gap-2">
              {showViewInCalendar && (
                <Button
                  variant="secondary"
                  onClick={() => navigate('/appointments', { state: { jumpToDate: a.startUtc, appointmentId: a.id } })}
                >
                  View in calendar
                </Button>
              )}
              <Button variant="secondary" onClick={onClose}>
                Close
              </Button>
              <Button onClick={() => setEditing(true)}>Edit</Button>
            </div>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={confirmingDelete}
        title="Delete appointment"
        message={`Delete "${a.title}"? This cannot be undone.`}
        onConfirm={handleDelete}
        onCancel={() => setConfirmingDelete(false)}
      />
    </>
  )
}

interface QuickCustomerFormProps {
  appointment: Appointment
  onDone: (updated: Appointment) => void
  onCancel: () => void
}

/** Minimal customer creation, prefilled from the appointment's phone-call contact. */
function QuickCustomerForm({ appointment, onDone, onCancel }: QuickCustomerFormProps) {
  const toast = useToast()
  // Best-effort split of "Dave Miller" into first/last for the receptionist to fix up.
  const nameParts = (appointment.contactName ?? '').trim().split(/\s+/)
  const [firstName, setFirstName] = useState(nameParts[0] ?? '')
  const [lastName, setLastName] = useState(nameParts.slice(1).join(' '))
  const [phoneNumber, setPhoneNumber] = useState(appointment.contactPhone ?? '')
  const [emailAddress, setEmailAddress] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!firstName.trim() || !lastName.trim() || !phoneNumber.trim()) {
      setError('First name, last name and phone are required.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const customer = await createCustomer({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phoneNumber: phoneNumber.trim(),
        emailAddress: emailAddress.trim() || null,
        physicalAddress: null,
        notes: null,
      })
      const updated = await updateAppointment(
        appointment.id,
        toAppointmentRequest(appointment, { customerId: customer.id }),
      )
      toast.success('Customer created and linked.')
      onDone(updated)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="First name" required>
          <input type="text" value={firstName} onChange={(e) => setFirstName(e.target.value.toUpperCase())} className={controlClass} />
        </Field>
        <Field label="Last name" required>
          <input type="text" value={lastName} onChange={(e) => setLastName(e.target.value.toUpperCase())} className={controlClass} />
        </Field>
        <Field label="Phone" required>
          <input type="tel" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Email">
          <input type="email" value={emailAddress} onChange={(e) => setEmailAddress(e.target.value)} className={controlClass} />
        </Field>
      </div>

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Creating…' : 'Create & link'}
        </Button>
      </div>
    </form>
  )
}
