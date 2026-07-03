import { useEffect, useState } from 'react'
import { getCustomers } from '@/api/customers'
import { getCars } from '@/api/cars'
import { getJobs } from '@/api/jobs'
import { getEmployees } from '@/api/employees'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from './controls'
import { APPOINTMENT_STATUS_LABELS, AppointmentStatus, JOB_STATUS_LABELS } from '@/types'
import type { Appointment, Car, CreateAppointmentRequest, Customer, Employee, Job } from '@/types'

interface AppointmentFormProps {
  initial: Appointment | null
  /** Prefill for the time fields when booking from a clicked calendar slot. */
  initialSlot?: { start: Date; end: Date }
  onSubmit: (values: CreateAppointmentRequest) => Promise<void>
  onCancel: () => void
}

const STATUS_OPTIONS = Object.entries(APPOINTMENT_STATUS_LABELS).map(([value, label]) => ({
  value,
  label,
}))

/** "yyyy-mm-dd" in local time, for a date input. */
const toDateInput = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** "hh:mm" in local time, for a time input. */
const toTimeInput = (d: Date) =>
  `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`

/**
 * Bespoke appointment form. Two booking modes:
 * - "Existing customer": customer → car cascade, plus an optional open-job link.
 * - "New caller": free-text contact details only — converted to real records at check-in.
 */
export function AppointmentForm({ initial, initialSlot, onSubmit, onCancel }: AppointmentFormProps) {
  const [mode, setMode] = useState<'existing' | 'caller'>(
    initial && !initial.customerId ? 'caller' : initial ? 'existing' : 'caller',
  )

  const [customers, setCustomers] = useState<Customer[]>([])
  const [cars, setCars] = useState<Car[]>([])
  const [jobs, setJobs] = useState<Job[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])

  const [customerId, setCustomerId] = useState(initial?.customerId ?? '')
  const [carId, setCarId] = useState(initial?.carId ?? '')
  const [jobId, setJobId] = useState(initial?.jobId ?? '')
  const [mechanicId, setMechanicId] = useState(initial?.mechanicId ?? '')

  const [contactName, setContactName] = useState(initial?.contactName ?? '')
  const [contactPhone, setContactPhone] = useState(initial?.contactPhone ?? '')
  const [vehicleDescription, setVehicleDescription] = useState(initial?.vehicleDescription ?? '')

  const start = initial ? new Date(initial.startUtc) : initialSlot?.start
  const end = initial ? new Date(initial.endUtc) : initialSlot?.end
  const [title, setTitle] = useState(initial?.title ?? '')
  const [date, setDate] = useState(start ? toDateInput(start) : toDateInput(new Date()))
  const [startTime, setStartTime] = useState(start ? toTimeInput(start) : '09:00')
  const [endTime, setEndTime] = useState(end ? toTimeInput(end) : '10:00')
  const [status, setStatus] = useState(String(initial?.status ?? AppointmentStatus.Scheduled))
  const [notes, setNotes] = useState(initial?.notes ?? '')

  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    getCustomers().then(setCustomers).catch(() => setCustomers([]))
    getEmployees().then(setEmployees).catch(() => setEmployees([]))
  }, [])

  // The selected customer scopes both the car list and the linkable jobs (cascade).
  useEffect(() => {
    if (!customerId) {
      setCars([])
      setJobs([])
      return
    }
    getCars(customerId).then(setCars).catch(() => setCars([]))
    getJobs(customerId).then(setJobs).catch(() => setJobs([]))
  }, [customerId])

  const handleCustomerChange = (value: string) => {
    setCustomerId(value)
    setCarId('')
    setJobId('')
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!title.trim()) {
      setError('Title is required.')
      return
    }
    if (mode === 'existing' && !customerId) {
      setError('Pick a customer, or switch to "New caller".')
      return
    }
    if (mode === 'caller' && (!contactName.trim() || !contactPhone.trim())) {
      setError('Contact name and phone are required for a new caller.')
      return
    }
    const startAt = new Date(`${date}T${startTime}`)
    const endAt = new Date(`${date}T${endTime}`)
    if (!(startAt < endAt)) {
      setError('End time must be after the start time.')
      return
    }

    setBusy(true)
    setError(null)
    try {
      await onSubmit({
        title: title.trim(),
        startUtc: startAt.toISOString(),
        endUtc: endAt.toISOString(),
        status: Number(status) as AppointmentStatus,
        notes: notes.trim() || null,
        contactName: contactName.trim() || null,
        contactPhone: contactPhone.trim() || null,
        vehicleDescription: vehicleDescription.trim() || null,
        customerId: mode === 'existing' ? customerId : null,
        carId: mode === 'existing' && carId ? carId : null,
        jobId: mode === 'existing' && jobId ? jobId : null,
        mechanicId: mechanicId || null,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const modeTabClass = (active: boolean) =>
    `flex-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
      active ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'
    }`

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="flex rounded-lg bg-slate-100 p-1">
        <button type="button" className={modeTabClass(mode === 'existing')} onClick={() => setMode('existing')}>
          Existing customer
        </button>
        <button type="button" className={modeTabClass(mode === 'caller')} onClick={() => setMode('caller')}>
          New caller
        </button>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {mode === 'existing' ? (
          <>
            <Field label="Customer" required>
              <select value={customerId} onChange={(e) => handleCustomerChange(e.target.value)} className={controlClass}>
                <option value="">Select…</option>
                {customers.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.firstName} {c.lastName}
                  </option>
                ))}
              </select>
            </Field>

            <Field label="Car">
              <select
                value={carId}
                onChange={(e) => setCarId(e.target.value)}
                disabled={!customerId}
                className={controlClass}
              >
                <option value="">{customerId ? 'None yet' : 'Pick a customer first'}</option>
                {cars.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.carMakeName} {c.carModelName} — {c.rego}
                  </option>
                ))}
              </select>
            </Field>

            <Field label="Link to job" className="sm:col-span-2">
              <select
                value={jobId}
                onChange={(e) => setJobId(e.target.value)}
                disabled={!customerId}
                className={controlClass}
              >
                <option value="">{customerId ? 'None — new visit' : 'Pick a customer first'}</option>
                {jobs.map((j) => (
                  <option key={j.id} value={j.id}>
                    {j.title} ({JOB_STATUS_LABELS[j.status]})
                  </option>
                ))}
              </select>
            </Field>
          </>
        ) : (
          <>
            <Field label="Contact name" required>
              <input type="text" value={contactName} onChange={(e) => setContactName(e.target.value)} className={controlClass} />
            </Field>
            <Field label="Phone" required>
              <input type="tel" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} className={controlClass} />
            </Field>
            <Field label="Vehicle (free text)" className="sm:col-span-2">
              <input
                type="text"
                value={vehicleDescription}
                onChange={(e) => setVehicleDescription(e.target.value)}
                placeholder="e.g. White 2014 Hilux, rego ABC123"
                className={controlClass}
              />
            </Field>
          </>
        )}

        <Field label="Title" required className="sm:col-span-2">
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Reason for the visit"
            className={controlClass}
          />
        </Field>

        <Field label="Date" required>
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)} className={controlClass} />
        </Field>
        <div className="grid grid-cols-2 gap-4">
          <Field label="Start" required>
            <input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} className={controlClass} />
          </Field>
          <Field label="End" required>
            <input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} className={controlClass} />
          </Field>
        </div>

        <Field label="Mechanic">
          <select value={mechanicId} onChange={(e) => setMechanicId(e.target.value)} className={controlClass}>
            <option value="">Unassigned</option>
            {employees.map((emp) => (
              <option key={emp.id} value={emp.id}>
                {emp.firstName} {emp.lastName}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Status">
          <select value={status} onChange={(e) => setStatus(e.target.value)} className={controlClass}>
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Notes" className="sm:col-span-2">
          <textarea value={notes} rows={2} onChange={(e) => setNotes(e.target.value)} className={controlClass} />
        </Field>
      </div>

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </form>
  )
}
