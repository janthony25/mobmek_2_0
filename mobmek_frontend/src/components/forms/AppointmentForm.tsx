import { useEffect, useState } from 'react'
import { getCustomersPaged } from '@/api/customers'
import { getCars } from '@/api/cars'
import { getJobs, getJobsPaged } from '@/api/jobs'
import { getEmployees } from '@/api/employees'
import { Button } from '@/components/ui/Button'
import { AsyncCombobox } from './AsyncCombobox'
import { Field, controlClass } from './controls'
import { APPOINTMENT_STATUS_LABELS, AppointmentStatus, JOB_STATUS_LABELS } from '@/types'
import type { Appointment, Car, CreateAppointmentRequest, Employee, Job } from '@/types'

/** Number of results shown in the customer search picker. */
const PICKER_PAGE_SIZE = 8

/** Job picker: only the most recently created jobs by default (server already returns newest-first). */
const JOB_PICKER_DEFAULT_SIZE = 6
/** Wider cap once the user searches by rego/customer/title — no longer just "most recent". */
const JOB_PICKER_SEARCH_SIZE = 20

const searchCustomers = (query: string) =>
  getCustomersPaged(1, PICKER_PAGE_SIZE, query).then((r) =>
    r.items.map((c) => ({ value: c.id, label: `${c.firstName} ${c.lastName}` })),
  )

interface AppointmentFormProps {
  initial: Appointment | null
  /** Prefill for the time fields when booking from a clicked calendar slot. */
  initialSlot?: { start: Date; end: Date }
  /** Prefill the "Existing job" tab when booking an appointment from a job's page. */
  initialJob?: Job
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
 * Bespoke appointment form. Three booking modes:
 * - "Existing customer": customer → car cascade, plus an optional open-job link.
 * - "Existing job": search jobs (by rego, customer, title, make/model) and pick a job
 *   card — customer/car are derived from the picked job.
 * - "New caller": free-text contact details only — converted to real records at check-in.
 */
export function AppointmentForm({ initial, initialSlot, initialJob, onSubmit, onCancel }: AppointmentFormProps) {
  const [mode, setMode] = useState<'existing' | 'job' | 'caller'>(
    initial && !initial.customerId ? 'caller' : initial ? 'existing' : initialJob ? 'job' : 'caller',
  )

  const [cars, setCars] = useState<Car[]>([])
  const [jobs, setJobs] = useState<Job[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])

  const [customerId, setCustomerId] = useState(initial?.customerId ?? initialJob?.customerId ?? '')
  const [carId, setCarId] = useState(initial?.carId ?? initialJob?.carId ?? '')
  const [jobId, setJobId] = useState(initial?.jobId ?? initialJob?.id ?? '')
  const [mechanicId, setMechanicId] = useState(initial?.mechanicId ?? initialJob?.mechanics[0]?.employeeId ?? '')

  const [selectedJob, setSelectedJob] = useState<Job | null>(initialJob ?? null)
  const [jobSearchQuery, setJobSearchQuery] = useState('')
  const [jobSearchResults, setJobSearchResults] = useState<Job[]>([])
  const [jobSearchLoading, setJobSearchLoading] = useState(false)

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

  // Debounced job search for the "Existing job" picker — a bounded page from the
  // server (already matches title/customer/make/model/rego), not the whole table.
  useEffect(() => {
    if (mode !== 'job' || selectedJob) return
    let cancelled = false
    setJobSearchLoading(true)
    const trimmedQuery = jobSearchQuery.trim()
    const handle = setTimeout(() => {
      getJobsPaged(1, trimmedQuery ? JOB_PICKER_SEARCH_SIZE : JOB_PICKER_DEFAULT_SIZE, trimmedQuery)
        .then((r) => {
          if (!cancelled) setJobSearchResults(r.items)
        })
        .catch(() => {
          if (!cancelled) setJobSearchResults([])
        })
        .finally(() => {
          if (!cancelled) setJobSearchLoading(false)
        })
    }, 300)
    return () => {
      cancelled = true
      clearTimeout(handle)
    }
  }, [mode, selectedJob, jobSearchQuery])

  const handleCustomerChange = (value: string) => {
    setCustomerId(value)
    setCarId('')
    setJobId('')
  }

  const handleJobPick = (job: Job) => {
    setSelectedJob(job)
    setCustomerId(job.customerId)
    setCarId(job.carId)
    setJobId(job.id)
    // The job already required a mechanic to be created — carry it over rather than
    // leaving the appointment unassigned.
    setMechanicId(job.mechanics[0]?.employeeId ?? '')
  }

  const handleJobClear = () => {
    setSelectedJob(null)
    setCustomerId('')
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
    if (mode === 'job' && !selectedJob) {
      setError('Pick a job, or switch to another tab.')
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
        customerId: mode === 'existing' || mode === 'job' ? customerId : null,
        carId: (mode === 'existing' || mode === 'job') && carId ? carId : null,
        jobId: (mode === 'existing' || mode === 'job') && jobId ? jobId : null,
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
        <button type="button" className={modeTabClass(mode === 'job')} onClick={() => setMode('job')}>
          Existing job
        </button>
        <button type="button" className={modeTabClass(mode === 'caller')} onClick={() => setMode('caller')}>
          New caller
        </button>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {mode === 'existing' ? (
          <>
            <Field label="Customer" required>
              <AsyncCombobox
                value={customerId}
                onChange={handleCustomerChange}
                search={searchCustomers}
                initialLabel={initial?.customerName}
                placeholder="Type to search customers…"
                emptyText="No matching customers"
              />
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
        ) : mode === 'job' ? (
          <div className="sm:col-span-2 space-y-3">
            {selectedJob ? (
              <div className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm">
                <div>
                  <p className="font-medium text-slate-900">{selectedJob.title}</p>
                  <p className="text-slate-500">
                    {selectedJob.customerName} — {selectedJob.carDescription}
                  </p>
                  <p className="mt-1 text-xs text-slate-400">{JOB_STATUS_LABELS[selectedJob.status]}</p>
                </div>
                <Button type="button" variant="secondary" onClick={handleJobClear}>
                  Change
                </Button>
              </div>
            ) : (
              <>
                <Field label="Search jobs" required>
                  <input
                    type="text"
                    value={jobSearchQuery}
                    onChange={(e) => setJobSearchQuery(e.target.value)}
                    placeholder="Search by rego#, customer, title, make/model…"
                    className={controlClass}
                  />
                </Field>
                {jobSearchLoading ? (
                  <p className="text-sm text-slate-400">Searching…</p>
                ) : jobSearchResults.length === 0 ? (
                  <p className="text-sm text-slate-400">No jobs match.</p>
                ) : (
                  <>
                    {!jobSearchQuery.trim() && (
                      <p className="text-xs text-slate-400">
                        Showing the {JOB_PICKER_DEFAULT_SIZE} most recent jobs — search by rego# to find others.
                      </p>
                    )}
                    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                      {jobSearchResults.map((j) => (
                        <button
                          key={j.id}
                          type="button"
                          onClick={() => handleJobPick(j)}
                          className="rounded-lg border border-slate-200 bg-white p-3 text-left text-sm shadow-sm transition hover:border-slate-400 hover:shadow-md"
                        >
                          <p className="font-medium text-slate-900">{j.title}</p>
                          <p className="text-slate-500">{j.customerName}</p>
                          <p className="text-slate-500">{j.carDescription}</p>
                          <p className="mt-1 text-xs text-slate-400">{JOB_STATUS_LABELS[j.status]}</p>
                        </button>
                      ))}
                    </div>
                  </>
                )}
              </>
            )}
          </div>
        ) : (
          <>
            <Field label="Contact name" required>
              <input type="text" value={contactName} onChange={(e) => setContactName(e.target.value.toUpperCase())} className={controlClass} />
            </Field>
            <Field label="Phone" required>
              <input type="tel" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} className={controlClass} />
            </Field>
            <Field label="Vehicle (free text)" className="sm:col-span-2">
              <input
                type="text"
                value={vehicleDescription}
                onChange={(e) => setVehicleDescription(e.target.value.toUpperCase())}
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
            onChange={(e) => setTitle(e.target.value.toUpperCase())}
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
