import { useEffect, useState } from 'react'
import { getCustomers } from '@/api/customers'
import { getCars } from '@/api/cars'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from './controls'
import { DiscountType, JOB_STATUS_LABELS, JobStatus } from '@/types'
import type { Car, Customer, Job } from '@/types'

interface JobFormProps {
  initial: Job | null
  onSubmit: (values: Record<string, unknown>) => Promise<void>
  onCancel: () => void
}

const STATUS_OPTIONS = Object.entries(JOB_STATUS_LABELS).map(([value, label]) => ({
  value,
  label,
}))

/** Bespoke job form: pick a customer, then one of their cars (cascade). */
export function JobForm({ initial, onSubmit, onCancel }: JobFormProps) {
  const isEdit = initial !== null
  const [customers, setCustomers] = useState<Customer[]>([])
  const [cars, setCars] = useState<Car[]>([])

  const [customerId, setCustomerId] = useState(initial?.customerId ?? '')
  const [carId, setCarId] = useState(initial?.carId ?? '')
  const [title, setTitle] = useState(initial?.title ?? '')
  const [status, setStatus] = useState(String(initial?.status ?? JobStatus.Open))
  const [odometer, setOdometer] = useState(initial ? String(initial.odometer) : '0')
  const [jobNotes, setJobNotes] = useState(initial?.jobNotes ?? '')
  const [invoiceNotes, setInvoiceNotes] = useState(initial?.invoiceNotes ?? '')

  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!isEdit) getCustomers().then(setCustomers).catch(() => setCustomers([]))
  }, [isEdit])

  // Load the selected customer's cars (cascade).
  useEffect(() => {
    if (!customerId) {
      setCars([])
      return
    }
    getCars(customerId).then(setCars).catch(() => setCars([]))
  }, [customerId])

  const handleCustomerChange = (value: string) => {
    setCustomerId(value)
    setCarId('')
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!customerId || !carId || !title.trim()) {
      setError('Customer, car and title are required.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await onSubmit({
        customerId,
        carId,
        title: title.trim(),
        status: Number(status),
        odometer: odometer.trim() === '' ? 0 : Number(odometer),
        jobNotes: jobNotes.trim() || null,
        invoiceNotes: invoiceNotes.trim() || null,
        // This quick-edit form doesn't surface the discount; carry the existing value
        // through unchanged so saving here doesn't silently clear it.
        discountType: initial?.discountType ?? DiscountType.None,
        discountValue: initial?.discountValue ?? 0,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Customer" required>
          {isEdit ? (
            <input
              type="text"
              value={initial?.customerName ?? 'Customer'}
              disabled
              className={`${controlClass} bg-slate-50 text-slate-500`}
            />
          ) : (
            <select value={customerId} onChange={(e) => handleCustomerChange(e.target.value)} className={controlClass}>
              <option value="">Select…</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.firstName} {c.lastName}
                </option>
              ))}
            </select>
          )}
        </Field>

        <Field label="Car" required>
          <select
            value={carId}
            onChange={(e) => setCarId(e.target.value)}
            disabled={!customerId}
            className={controlClass}
          >
            <option value="">{customerId ? 'Select…' : 'Pick a customer first'}</option>
            {cars.map((c) => (
              <option key={c.id} value={c.id}>
                {c.carMakeName} {c.carModelName} — {c.rego}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Title" required className="sm:col-span-2">
          <input type="text" value={title} onChange={(e) => setTitle(e.target.value.toUpperCase())} className={controlClass} />
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
        <Field label="Odometer">
          <input type="number" min={0} value={odometer} onChange={(e) => setOdometer(e.target.value)} className={controlClass} />
        </Field>

        <Field label="Job notes" className="sm:col-span-2">
          <textarea value={jobNotes ?? ''} rows={2} onChange={(e) => setJobNotes(e.target.value)} className={controlClass} />
        </Field>
        <Field label="Invoice notes" className="sm:col-span-2">
          <textarea value={invoiceNotes ?? ''} rows={2} onChange={(e) => setInvoiceNotes(e.target.value)} className={controlClass} />
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
