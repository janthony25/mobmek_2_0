import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { getCustomers } from '@/api/customers'
import { getCars } from '@/api/cars'
import { getEmployees } from '@/api/employees'
import { getJobServices } from '@/api/jobServices'
import { addJobMechanic, createJob } from '@/api/jobs'
import { createJobItem } from '@/api/jobItems'
import { createLabour } from '@/api/labour'
import { createJobServiceLine } from '@/api/jobServiceLines'
import { Button } from '@/components/ui/Button'
import { Field, controlClass } from '@/components/forms/controls'
import { Combobox } from '@/components/forms/Combobox'
import { useToast } from '@/components/ui/toast'
import { currency } from '@/lib/format'
import {
  JOB_STATUS_LABELS,
  JobStatus,
  MARKUP_SOLUTION_LABELS,
  MarkupSolution,
  type Car,
  type CreateJobRequest,
  type Customer,
  type Employee,
  type JobService,
} from '@/types'

// ── numeric helpers ───────────────────────────────────────────────────────────

const num = (s: string): number | null => {
  const t = s.trim()
  if (t === '') return null
  const n = Number(t)
  return Number.isFinite(n) ? n : null
}

const round2 = (n: number) => (Math.sign(n) * Math.round(Math.abs(n) * 100)) / 100

// ── draft row shapes ───────────────────────────────────────────────────────────

interface PartDraft {
  key: string
  itemName: string
  tradePrice: string
  retailPrice: string
  markupSolution: number
  markup: string
  sellingPrice: string
  itemQuantity: string
}

interface LabourDraft {
  key: string
  hours: string
  ratePerHour: string
  fixedAmount: string
}

const newKey = () => Math.random().toString(36).slice(2)

const emptyPart = (): PartDraft => ({
  key: newKey(),
  itemName: '',
  tradePrice: '',
  retailPrice: '',
  markupSolution: MarkupSolution.Percentage,
  markup: '0',
  sellingPrice: '',
  itemQuantity: '1',
})

const emptyLabour = (): LabourDraft => ({ key: newKey(), hours: '', ratePerHour: '', fixedAmount: '' })

/** Mirrors the backend's JobItemService.Apply so the operator sees live figures. */
function computePart(p: PartDraft) {
  const trade = num(p.tradePrice)
  const retail = num(p.retailPrice)
  const markup = num(p.markup) ?? 0
  const qty = num(p.itemQuantity) ?? 0
  // Selling price is the markup applied to the retail price; the trade price is the cost,
  // used only to derive profit. Without a retail price, the manual selling price is used.
  const selling = round2(
    retail != null
      ? p.markupSolution === MarkupSolution.Percentage
        ? retail * (1 + markup / 100)
        : retail + markup
      : num(p.sellingPrice) ?? 0,
  )
  const unitProfit = round2(selling - (trade ?? 0))
  return { unitPrice: selling, itemTotal: round2(selling * qty), rowProfit: round2(unitProfit * qty) }
}

function computeLabour(l: LabourDraft) {
  const fixed = num(l.fixedAmount)
  return round2(fixed != null ? fixed : (num(l.hours) ?? 0) * (num(l.ratePerHour) ?? 0))
}

const STATUS_OPTIONS = Object.entries(JOB_STATUS_LABELS).map(([value, label]) => ({ value, label }))

export function NewJobPage() {
  const toast = useToast()
  const navigate = useNavigate()
  // Optionally pre-selected when arriving from a customer's car page.
  const preset = (useLocation().state ?? {}) as { customerId?: string; carId?: string }

  const [customers, setCustomers] = useState<Customer[]>([])
  const [cars, setCars] = useState<Car[]>([])
  const [employees, setEmployees] = useState<Employee[]>([])
  const [services, setServices] = useState<JobService[]>([])

  // Job fields
  const [customerId, setCustomerId] = useState(preset.customerId ?? '')
  const [carId, setCarId] = useState(preset.carId ?? '')
  const [title, setTitle] = useState('')
  const [status, setStatus] = useState(String(JobStatus.Open))
  const [odometer, setOdometer] = useState('0')
  const [jobNotes, setJobNotes] = useState('')
  const [invoiceNotes, setInvoiceNotes] = useState('')

  // Children
  const [mechanicIds, setMechanicIds] = useState<string[]>([])
  const [mechanicPick, setMechanicPick] = useState('')
  const [serviceIds, setServiceIds] = useState<Set<string>>(new Set())
  const [parts, setParts] = useState<PartDraft[]>([])
  const [labour, setLabour] = useState<LabourDraft[]>([])

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getCustomers().then(setCustomers).catch(() => setCustomers([]))
    getEmployees().then(setEmployees).catch(() => setEmployees([]))
    getJobServices()
      .then((s) => setServices(s.filter((x) => x.isActive)))
      .catch(() => setServices([]))
  }, [])

  useEffect(() => {
    if (!customerId) {
      setCars([])
      return
    }
    getCars(customerId).then(setCars).catch(() => setCars([]))
  }, [customerId])

  const availableMechanics = employees.filter((e) => !mechanicIds.includes(e.id))
  const nameOf = (id: string) => {
    const e = employees.find((x) => x.id === id)
    return e ? `${e.firstName} ${e.lastName}` : id
  }

  const estimatedTotal = useMemo(() => {
    const partsTotal = parts.reduce((sum, p) => sum + computePart(p).itemTotal, 0)
    const labourTotal = labour.reduce((sum, l) => sum + computeLabour(l), 0)
    const servicesTotal = services
      .filter((s) => serviceIds.has(s.id))
      .reduce((sum, s) => sum + s.price, 0)
    return round2(partsTotal + labourTotal + servicesTotal)
  }, [parts, labour, services, serviceIds])

  const toggleService = (id: string) =>
    setServiceIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const updatePart = (key: string, patch: Partial<PartDraft>) =>
    setParts((prev) => prev.map((p) => (p.key === key ? { ...p, ...patch } : p)))
  const updateLabour = (key: string, patch: Partial<LabourDraft>) =>
    setLabour((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const save = async () => {
    if (!customerId || !carId || !title.trim()) {
      setError('Customer, car and title are required.')
      return
    }
    setError(null)
    setBusy(true)

    const jobBody: CreateJobRequest = {
      customerId,
      carId,
      title: title.trim(),
      status: Number(status) as JobStatus,
      odometer: num(odometer) ?? 0,
      jobNotes: jobNotes.trim() || null,
      invoiceNotes: invoiceNotes.trim() || null,
    }

    let jobId: string
    try {
      const job = await createJob(jobBody)
      jobId = job.id
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
      setBusy(false)
      return
    }

    // The job exists now; add its lines, tolerating individual failures so one bad
    // row doesn't strand the whole job. Anything that fails is reported and can be
    // fixed on the job's detail page.
    const failures: string[] = []
    const attempt = async (label: string, fn: () => Promise<unknown>) => {
      try {
        await fn()
      } catch (err) {
        failures.push(`${label}: ${err instanceof Error ? err.message : String(err)}`)
      }
    }

    for (const id of mechanicIds) await attempt(`Mechanic ${nameOf(id)}`, () => addJobMechanic(jobId, id))
    for (const id of serviceIds)
      await attempt('Service', () => createJobServiceLine(jobId, { jobServiceId: id, quantity: 1 }))
    for (const p of parts.filter((x) => x.itemName.trim() !== ''))
      await attempt(`Part ${p.itemName}`, () =>
        createJobItem(jobId, {
          itemName: p.itemName.trim(),
          tradePrice: num(p.tradePrice),
          retailPrice: num(p.retailPrice),
          markupSolution: p.markupSolution as MarkupSolution,
          markup: num(p.markup) ?? 0,
          itemQuantity: num(p.itemQuantity) ?? 1,
          sellingPrice: num(p.sellingPrice),
        }),
      )
    for (const l of labour.filter(
      (x) => num(x.fixedAmount) != null || num(x.hours) != null || num(x.ratePerHour) != null,
    ))
      await attempt('Labour', () =>
        createLabour(jobId, {
          hours: num(l.hours),
          ratePerHour: num(l.ratePerHour),
          fixedAmount: num(l.fixedAmount),
        }),
      )

    setBusy(false)
    if (failures.length > 0) {
      toast.error(`Job created, but some lines failed: ${failures.join('; ')}`)
    } else {
      toast.success('Job created')
    }
    navigate(`/jobs/${jobId}`)
  }

  return (
    <div className="space-y-6 pb-24">
      <div>
        <Link to="/jobs" className="text-sm text-slate-500 hover:underline">
          ← Back to Job Center
        </Link>
        <h1 className="mt-2 text-2xl font-semibold text-slate-900">New Job</h1>
      </div>

      {/* Job details + notes */}
      <section className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Details</h2>
          <Field label="Customer" required>
            <Combobox
              options={customers.map((c) => ({
                value: c.id,
                label: `${c.firstName} ${c.lastName}`,
              }))}
              value={customerId}
              onChange={(id) => {
                setCustomerId(id)
                setCarId('')
              }}
              placeholder="Type to search customers…"
              emptyText="No matching customers"
            />
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
          <Field label="Title" required>
            <input type="text" value={title} onChange={(e) => setTitle(e.target.value)} className={controlClass} />
          </Field>
          <div className="grid grid-cols-2 gap-4">
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
          </div>

          <Field label="Mechanics">
            <div className="space-y-2">
              {mechanicIds.length > 0 && (
                <ul className="flex flex-wrap gap-2">
                  {mechanicIds.map((id) => (
                    <li
                      key={id}
                      className="inline-flex items-center gap-2 rounded-full bg-slate-100 py-1 pl-3 pr-1 text-sm text-slate-700"
                    >
                      {nameOf(id)}
                      <button
                        type="button"
                        onClick={() => setMechanicIds((prev) => prev.filter((x) => x !== id))}
                        className="rounded-full px-1.5 text-slate-400 hover:bg-slate-200 hover:text-slate-700"
                        aria-label={`Remove ${nameOf(id)}`}
                      >
                        ✕
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <div className="flex items-center gap-2">
                <select value={mechanicPick} onChange={(e) => setMechanicPick(e.target.value)} className={controlClass}>
                  <option value="">Add a mechanic…</option>
                  {availableMechanics.map((e) => (
                    <option key={e.id} value={e.id}>
                      {e.firstName} {e.lastName}
                    </option>
                  ))}
                </select>
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  disabled={!mechanicPick}
                  onClick={() => {
                    if (!mechanicPick) return
                    setMechanicIds((prev) => [...prev, mechanicPick])
                    setMechanicPick('')
                  }}
                >
                  Add
                </Button>
              </div>
            </div>
          </Field>

          <Field label="Job notes">
            <textarea value={jobNotes} rows={3} onChange={(e) => setJobNotes(e.target.value)} className={controlClass} />
          </Field>
        </div>

        <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Invoice notes</h2>
          <Field label="Invoice notes">
            <textarea
              value={invoiceNotes}
              rows={6}
              onChange={(e) => setInvoiceNotes(e.target.value)}
              className={controlClass}
              placeholder="Shown on invoices generated from this job."
            />
          </Field>
        </div>
      </section>

      {/* Services */}
      <section className="rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Services</h2>
        {services.length === 0 ? (
          <p className="text-sm text-slate-500">No active catalog services. Add some under Catalog → Services.</p>
        ) : (
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {services.map((s) => (
              <label
                key={s.id}
                className="flex items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm hover:bg-slate-50"
              >
                <input type="checkbox" checked={serviceIds.has(s.id)} onChange={() => toggleService(s.id)} />
                <span className="flex-1 text-slate-700">{s.name}</span>
                <span className="text-slate-500">{currency(s.price)}</span>
              </label>
            ))}
          </div>
        )}
      </section>

      {/* Parts */}
      <section className="rounded-lg border border-slate-200 bg-white p-5">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Parts &amp; Items</h2>
          <Button type="button" variant="secondary" size="sm" onClick={() => setParts((p) => [...p, emptyPart()])}>
            + Add part
          </Button>
        </div>
        {parts.length === 0 ? (
          <p className="text-sm text-slate-500">No parts yet. Use “Add part” to add one.</p>
        ) : (
          <div className="space-y-3">
            {parts.map((p) => {
              const c = computePart(p)
              return (
                <div key={p.key} className="rounded-md border border-slate-200 p-3">
                  <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                    <Field label="Item name" required className="col-span-2">
                      <input value={p.itemName} onChange={(e) => updatePart(p.key, { itemName: e.target.value })} className={controlClass} />
                    </Field>
                    <Field label="Quantity">
                      <input type="number" min={1} value={p.itemQuantity} onChange={(e) => updatePart(p.key, { itemQuantity: e.target.value })} className={controlClass} />
                    </Field>
                    <Field label="Trade price">
                      <input type="number" step="0.01" min={0} value={p.tradePrice} onChange={(e) => updatePart(p.key, { tradePrice: e.target.value })} className={controlClass} />
                    </Field>
                    <Field label="Retail price">
                      <input type="number" step="0.01" min={0} value={p.retailPrice} onChange={(e) => updatePart(p.key, { retailPrice: e.target.value })} className={controlClass} />
                    </Field>
                    <Field label="Markup type">
                      <select
                        value={p.markupSolution}
                        onChange={(e) => updatePart(p.key, { markupSolution: Number(e.target.value) })}
                        className={controlClass}
                      >
                        {Object.entries(MARKUP_SOLUTION_LABELS).map(([value, label]) => (
                          <option key={value} value={value}>
                            {label}
                          </option>
                        ))}
                      </select>
                    </Field>
                    <Field label="Markup value">
                      <input type="number" step="0.01" min={0} value={p.markup} onChange={(e) => updatePart(p.key, { markup: e.target.value })} className={controlClass} />
                    </Field>
                    <Field label="Selling price" className="sm:col-span-2">
                      <input
                        type="number"
                        step="0.01"
                        min={0}
                        value={p.sellingPrice}
                        onChange={(e) => updatePart(p.key, { sellingPrice: e.target.value })}
                        disabled={num(p.retailPrice) != null}
                        className={`${controlClass} ${num(p.retailPrice) != null ? 'bg-slate-50 text-slate-400' : ''}`}
                        placeholder={num(p.retailPrice) != null ? 'Derived from markup' : 'Used when no retail price'}
                      />
                    </Field>
                  </div>
                  <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-3 text-sm">
                    <div className="flex flex-wrap gap-x-6 gap-y-1 text-slate-600">
                      <span>Unit price: <strong className="text-slate-900">{currency(c.unitPrice)}</strong></span>
                      <span>Item total: <strong className="text-slate-900">{currency(c.itemTotal)}</strong></span>
                      <span>Row profit: <strong className="text-slate-900">{currency(c.rowProfit)}</strong></span>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="text-red-600"
                      onClick={() => setParts((prev) => prev.filter((x) => x.key !== p.key))}
                    >
                      Remove
                    </Button>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </section>

      {/* Labour */}
      <section className="rounded-lg border border-slate-200 bg-white p-5">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Labour</h2>
          <Button type="button" variant="secondary" size="sm" onClick={() => setLabour((l) => [...l, emptyLabour()])}>
            + Add labour
          </Button>
        </div>
        {labour.length === 0 ? (
          <p className="text-sm text-slate-500">No labour yet. Use “Add labour” to add a line.</p>
        ) : (
          <div className="space-y-3">
            {labour.map((l) => (
              <div key={l.key} className="rounded-md border border-slate-200 p-3">
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <Field label="Hours">
                    <input type="number" step="0.1" min={0} value={l.hours} onChange={(e) => updateLabour(l.key, { hours: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Rate / hour">
                    <input type="number" step="0.01" min={0} value={l.ratePerHour} onChange={(e) => updateLabour(l.key, { ratePerHour: e.target.value })} className={controlClass} />
                  </Field>
                  <Field label="Fixed amount">
                    <input
                      type="number"
                      step="0.01"
                      min={0}
                      value={l.fixedAmount}
                      onChange={(e) => updateLabour(l.key, { fixedAmount: e.target.value })}
                      className={controlClass}
                      placeholder="Overrides hours × rate"
                    />
                  </Field>
                  <div className="flex items-end justify-between gap-2">
                    <div className="text-sm text-slate-600">
                      Total: <strong className="text-slate-900">{currency(computeLabour(l))}</strong>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="text-red-600"
                      onClick={() => setLabour((prev) => prev.filter((x) => x.key !== l.key))}
                    >
                      Remove
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {error && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

      {/* Action bar */}
      <div className="fixed inset-x-0 bottom-0 border-t border-slate-200 bg-white/95 px-6 py-3 backdrop-blur">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-4">
          <span className="text-sm text-slate-500">
            Estimated total: <strong className="text-slate-900">{currency(estimatedTotal)}</strong>
          </span>
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => navigate('/jobs')} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={save} disabled={busy}>
              {busy ? 'Creating…' : 'Create Job'}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
