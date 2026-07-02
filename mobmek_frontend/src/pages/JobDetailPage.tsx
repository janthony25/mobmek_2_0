import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getCars } from '@/api/cars'
import { getEmployees } from '@/api/employees'
import { addJobMechanic, getJob, removeJobMechanic, updateJob } from '@/api/jobs'
import { createJobItem, deleteJobItem, getJobItems, updateJobItem } from '@/api/jobItems'
import { createLabour, deleteLabour, getLabour, updateLabour } from '@/api/labour'
import {
  createJobServiceLine,
  deleteJobServiceLine,
  getJobServiceLines,
} from '@/api/jobServiceLines'
import { getJobServices } from '@/api/jobServices'
import { Field, controlClass } from '@/components/forms/controls'
import { InvoicesSection } from '@/components/jobs/InvoicesSection'
import { QuotationsSection } from '@/components/jobs/QuotationsSection'
import { PartsEditor } from '@/components/jobs/PartsEditor'
import { LabourEditor } from '@/components/jobs/LabourEditor'
import { RemindersSection } from '@/components/reminders/RemindersSection'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import { useAsync } from '@/hooks/useAsync'
import { currency, orDash } from '@/lib/format'
import {
  computeLabour,
  computePart,
  emptyLabour,
  emptyPart,
  newKey,
  num,
  round2,
  type LabourDraft,
  type PartDraft,
} from '@/lib/jobLineDrafts'
import { JOB_STATUS_LABELS, type MarkupSolution, type UpdateJobRequest } from '@/types'

const STATUS_OPTIONS = Object.entries(JOB_STATUS_LABELS).map(([value, label]) => ({ value, label }))

export function JobDetailPage() {
  const { id = '' } = useParams()
  const toast = useToast()

  const { data: job, loading, error, reload: reloadJob } = useAsync(() => getJob(id), [id])
  const itemsQuery = useAsync(() => getJobItems(id), [id])
  const labourQuery = useAsync(() => getLabour(id), [id])
  const linesQuery = useAsync(() => getJobServiceLines(id), [id])
  const servicesQuery = useAsync(() => getJobServices(), [])
  const employeesQuery = useAsync(getEmployees, [])
  const carsQuery = useAsync(() => (job ? getCars(job.customerId) : Promise.resolve([])), [job?.customerId])

  const [mode, setMode] = useState<'view' | 'edit'>('view')
  const [busy, setBusy] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  // Accepting a quotation creates an invoice; bump this to refresh the invoices list.
  const [invoicesReloadKey, setInvoicesReloadKey] = useState(0)

  // Draft state, seeded from the loaded job when "Edit" is pressed.
  const [carId, setCarId] = useState('')
  const [title, setTitle] = useState('')
  const [status, setStatus] = useState('')
  const [odometer, setOdometer] = useState('0')
  const [jobNotes, setJobNotes] = useState('')
  const [invoiceNotes, setInvoiceNotes] = useState('')
  const [mechanicIds, setMechanicIds] = useState<string[]>([])
  const [mechanicPick, setMechanicPick] = useState('')
  const [serviceIds, setServiceIds] = useState<Set<string>>(new Set())
  const [parts, setParts] = useState<PartDraft[]>([])
  const [removedPartIds, setRemovedPartIds] = useState<string[]>([])
  const [labour, setLabour] = useState<LabourDraft[]>([])
  const [removedLabourIds, setRemovedLabourIds] = useState<string[]>([])

  const dataReady =
    itemsQuery.data != null && labourQuery.data != null && linesQuery.data != null && employeesQuery.data != null

  const availableMechanics = (employeesQuery.data ?? []).filter((e) => !mechanicIds.includes(e.id))
  const nameOf = (employeeId: string) => {
    const fromJob = job?.mechanics.find((m) => m.employeeId === employeeId)?.fullName
    if (fromJob) return fromJob
    const e = (employeesQuery.data ?? []).find((x) => x.id === employeeId)
    return e ? `${e.firstName} ${e.lastName}` : employeeId
  }

  // Active services plus any inactive one still referenced by an existing line, so it
  // doesn't silently vanish from the grid for a job that already used it.
  const serviceCatalog = useMemo(() => {
    const all = servicesQuery.data ?? []
    const usedIds = new Set((linesQuery.data ?? []).map((l) => l.jobServiceId))
    return all.filter((s) => s.isActive || usedIds.has(s.id))
  }, [servicesQuery.data, linesQuery.data])

  const displayedServiceIds =
    mode === 'edit' ? serviceIds : new Set((linesQuery.data ?? []).map((l) => l.jobServiceId))

  const estimatedTotal = useMemo(() => {
    const partsTotal = parts.reduce((sum, p) => sum + computePart(p).itemTotal, 0)
    const labourTotal = labour.reduce((sum, l) => sum + computeLabour(l), 0)
    const servicesTotal = (servicesQuery.data ?? [])
      .filter((s) => serviceIds.has(s.id))
      .reduce((sum, s) => sum + s.price, 0)
    return round2(partsTotal + labourTotal + servicesTotal)
  }, [parts, labour, serviceIds, servicesQuery.data])

  const toggleService = (serviceId: string) =>
    setServiceIds((prev) => {
      const next = new Set(prev)
      if (next.has(serviceId)) next.delete(serviceId)
      else next.add(serviceId)
      return next
    })

  const updatePart = (key: string, patch: Partial<PartDraft>) =>
    setParts((prev) => prev.map((p) => (p.key === key ? { ...p, ...patch } : p)))
  const updateLabourDraft = (key: string, patch: Partial<LabourDraft>) =>
    setLabour((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const removePart = (key: string) =>
    setParts((prev) => {
      const target = prev.find((p) => p.key === key)
      if (target?.id) setRemovedPartIds((ids) => [...ids, target.id!])
      return prev.filter((p) => p.key !== key)
    })
  const removeLabour = (key: string) =>
    setLabour((prev) => {
      const target = prev.find((l) => l.key === key)
      if (target?.id) setRemovedLabourIds((ids) => [...ids, target.id!])
      return prev.filter((l) => l.key !== key)
    })

  const startEdit = () => {
    if (!job) return
    setCarId(job.carId)
    setTitle(job.title)
    setStatus(String(job.status))
    setOdometer(String(job.odometer))
    setJobNotes(job.jobNotes ?? '')
    setInvoiceNotes(job.invoiceNotes ?? '')
    setMechanicIds(job.mechanics.map((m) => m.employeeId))
    setMechanicPick('')
    setServiceIds(new Set((linesQuery.data ?? []).map((l) => l.jobServiceId)))
    setParts(
      (itemsQuery.data ?? []).map((i) => ({
        key: newKey(),
        id: i.id,
        itemName: i.itemName,
        tradePrice: i.tradePrice != null ? String(i.tradePrice) : '',
        retailPrice: i.retailPrice != null ? String(i.retailPrice) : '',
        markupSolution: i.markupSolution,
        markup: String(i.markup),
        sellingPrice: String(i.sellingPrice),
        itemQuantity: String(i.itemQuantity),
      })),
    )
    setRemovedPartIds([])
    setLabour(
      (labourQuery.data ?? []).map((l) => ({
        key: newKey(),
        id: l.id,
        hours: l.hours != null ? String(l.hours) : '',
        ratePerHour: l.ratePerHour != null ? String(l.ratePerHour) : '',
        fixedAmount: l.fixedAmount != null ? String(l.fixedAmount) : '',
      })),
    )
    setRemovedLabourIds([])
    setSaveError(null)
    setMode('edit')
  }

  const cancelEdit = () => {
    setMode('view')
    setSaveError(null)
  }

  const save = async () => {
    if (!job) return
    if (!carId || !title.trim()) {
      setSaveError('Car and title are required.')
      return
    }
    setSaveError(null)
    setBusy(true)

    const failures: string[] = []
    const attempt = async (label: string, fn: () => Promise<unknown>) => {
      try {
        await fn()
      } catch (err) {
        failures.push(`${label}: ${err instanceof Error ? err.message : String(err)}`)
      }
    }

    const jobBody: UpdateJobRequest = {
      carId,
      title: title.trim(),
      status: Number(status) as UpdateJobRequest['status'],
      odometer: num(odometer) ?? 0,
      jobNotes: jobNotes.trim() || null,
      invoiceNotes: invoiceNotes.trim() || null,
    }
    await attempt('Job details', () => updateJob(id, jobBody))

    const originalMechanicIds = job.mechanics.map((m) => m.employeeId)
    for (const empId of mechanicIds.filter((x) => !originalMechanicIds.includes(x)))
      await attempt(`Mechanic ${nameOf(empId)}`, () => addJobMechanic(id, empId))
    for (const empId of originalMechanicIds.filter((x) => !mechanicIds.includes(x)))
      await attempt(`Mechanic ${nameOf(empId)}`, () => removeJobMechanic(id, empId))

    const originalLines = linesQuery.data ?? []
    for (const serviceId of serviceIds)
      if (!originalLines.some((l) => l.jobServiceId === serviceId))
        await attempt('Service', () => createJobServiceLine(id, { jobServiceId: serviceId, quantity: 1 }))
    for (const line of originalLines)
      if (!serviceIds.has(line.jobServiceId))
        await attempt('Service', () => deleteJobServiceLine(id, line.id))

    for (const p of parts.filter((x) => x.itemName.trim() !== '')) {
      const body = {
        itemName: p.itemName.trim(),
        tradePrice: num(p.tradePrice),
        retailPrice: num(p.retailPrice),
        markupSolution: p.markupSolution as MarkupSolution,
        markup: num(p.markup) ?? 0,
        itemQuantity: num(p.itemQuantity) ?? 1,
        sellingPrice: num(p.sellingPrice),
      }
      if (p.id) await attempt(`Part ${p.itemName}`, () => updateJobItem(id, p.id!, body))
      else await attempt(`Part ${p.itemName}`, () => createJobItem(id, body))
    }
    for (const itemId of removedPartIds) await attempt('Part', () => deleteJobItem(id, itemId))

    for (const l of labour.filter(
      (x) => num(x.fixedAmount) != null || num(x.hours) != null || num(x.ratePerHour) != null,
    )) {
      const body = { hours: num(l.hours), ratePerHour: num(l.ratePerHour), fixedAmount: num(l.fixedAmount) }
      if (l.id) await attempt('Labour', () => updateLabour(id, l.id!, body))
      else await attempt('Labour', () => createLabour(id, body))
    }
    for (const labourId of removedLabourIds) await attempt('Labour', () => deleteLabour(id, labourId))

    setBusy(false)
    if (failures.length > 0) {
      toast.error(`Some changes failed: ${failures.join('; ')}`)
    } else {
      toast.success('Job updated')
    }
    reloadJob()
    itemsQuery.reload()
    labourQuery.reload()
    linesQuery.reload()
    setMode('view')
  }

  if (loading) return <StateMessage title="Loading job…" />
  if (error) return <StateMessage title="Could not load job" description={error.message} />
  if (!job) return <StateMessage title="Job not found" />

  return (
    <div className="space-y-6 pb-24">
      <InvoicesSection jobId={id} reloadKey={invoicesReloadKey} />

      <QuotationsSection jobId={id} onAccepted={() => setInvoicesReloadKey((k) => k + 1)} />

      <div className="rounded-xl border border-slate-200 border-l-4 border-l-amber-500 bg-amber-50/40 p-5 shadow-md">
        <RemindersSection
          customerId={job.customerId}
          lockedCarId={job.carId}
          title="⏰ Reminders"
          description="Reminders for this car, e.g. next WOF or service."
          collapsible
        />
      </div>

      <div>
        <Link to="/jobs" className="text-sm text-slate-500 hover:underline">
          ← Back to Job Center
        </Link>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold text-slate-900">{job.title}</h1>
          {mode === 'view' ? (
            <Button onClick={startEdit} disabled={!dataReady}>
              Edit
            </Button>
          ) : (
            <span className="text-sm font-medium text-slate-500">Editing…</span>
          )}
        </div>
        <dl className="mt-3 grid grid-cols-2 gap-x-8 gap-y-1 text-sm text-slate-600 sm:grid-cols-4">
          <Detail label="Total price" value={currency(job.totalJobPrice)} />
          <Detail label="Total profit" value={currency(job.totalJobProfit)} />
        </dl>
      </div>

      <section className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Details</h2>

          <Field label="Customer">
            <input
              type="text"
              value={job.customerName ?? ''}
              disabled
              className={`${controlClass} bg-slate-50 text-slate-500`}
            />
          </Field>

          {mode === 'edit' ? (
            <Field label="Car" required>
              <select value={carId} onChange={(e) => setCarId(e.target.value)} className={controlClass}>
                <option value="">Select…</option>
                {(carsQuery.data ?? []).map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.carMakeName} {c.carModelName} — {c.rego}
                  </option>
                ))}
              </select>
            </Field>
          ) : (
            <ReadField label="Car" value={orDash(job.carDescription)} />
          )}

          {mode === 'edit' ? (
            <Field label="Title" required>
              <input type="text" value={title} onChange={(e) => setTitle(e.target.value)} className={controlClass} />
            </Field>
          ) : (
            <ReadField label="Title" value={job.title} />
          )}

          <div className="grid grid-cols-2 gap-4">
            {mode === 'edit' ? (
              <Field label="Status">
                <select value={status} onChange={(e) => setStatus(e.target.value)} className={controlClass}>
                  {STATUS_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </Field>
            ) : (
              <ReadField label="Status" value={JOB_STATUS_LABELS[job.status]} />
            )}
            {mode === 'edit' ? (
              <Field label="Odometer">
                <input
                  type="number"
                  min={0}
                  value={odometer}
                  onChange={(e) => setOdometer(e.target.value)}
                  className={controlClass}
                />
              </Field>
            ) : (
              <ReadField label="Odometer" value={job.odometer.toLocaleString()} />
            )}
          </div>

          <Field label="Mechanics">
            <div className="space-y-2">
              {(mode === 'edit' ? mechanicIds : job.mechanics.map((m) => m.employeeId)).length > 0 ? (
                <ul className="flex flex-wrap gap-2">
                  {(mode === 'edit' ? mechanicIds : job.mechanics.map((m) => m.employeeId)).map((empId) => (
                    <li
                      key={empId}
                      className="inline-flex items-center gap-2 rounded-full bg-slate-100 py-1 pl-3 pr-1 text-sm text-slate-700"
                    >
                      {nameOf(empId)}
                      {mode === 'edit' && (
                        <button
                          type="button"
                          onClick={() => setMechanicIds((prev) => prev.filter((x) => x !== empId))}
                          className="rounded-full px-1.5 text-slate-400 hover:bg-slate-200 hover:text-slate-700"
                          aria-label={`Remove ${nameOf(empId)}`}
                        >
                          ✕
                        </button>
                      )}
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-slate-500">No mechanics assigned.</p>
              )}
              {mode === 'edit' && (
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
              )}
            </div>
          </Field>

          {mode === 'edit' ? (
            <Field label="Job notes">
              <textarea value={jobNotes} rows={3} onChange={(e) => setJobNotes(e.target.value)} className={controlClass} />
            </Field>
          ) : (
            <ReadField label="Job notes" value={job.jobNotes || '—'} />
          )}
        </div>

        <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-5">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Invoice notes</h2>
          {mode === 'edit' ? (
            <textarea
              value={invoiceNotes}
              rows={6}
              onChange={(e) => setInvoiceNotes(e.target.value)}
              className={controlClass}
              placeholder="Shown on invoices generated from this job."
            />
          ) : (
            <p className="whitespace-pre-wrap text-sm text-slate-900">{job.invoiceNotes || '—'}</p>
          )}
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Services</h2>
        {serviceCatalog.length === 0 ? (
          <p className="text-sm text-slate-500">No active catalog services. Add some under Catalog → Services.</p>
        ) : (
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {serviceCatalog.map((s) => (
              <label
                key={s.id}
                className={`flex items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm ${mode === 'edit' ? 'hover:bg-slate-50' : ''}`}
              >
                <input
                  type="checkbox"
                  checked={displayedServiceIds.has(s.id)}
                  disabled={mode !== 'edit'}
                  onChange={() => toggleService(s.id)}
                />
                <span className="flex-1 text-slate-700">{s.name}</span>
                <span className="text-slate-500">{currency(s.price)}</span>
              </label>
            ))}
          </div>
        )}
      </section>

      {mode === 'edit' ? (
        <PartsEditor parts={parts} onAdd={() => setParts((p) => [...p, emptyPart()])} onUpdate={updatePart} onRemove={removePart} />
      ) : (
        <PartsSummary items={itemsQuery.data ?? []} />
      )}

      {mode === 'edit' ? (
        <LabourEditor
          labour={labour}
          onAdd={() => setLabour((l) => [...l, emptyLabour()])}
          onUpdate={updateLabourDraft}
          onRemove={removeLabour}
        />
      ) : (
        <LabourSummary labour={labourQuery.data ?? []} />
      )}

      {saveError && <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{saveError}</p>}

      {mode === 'edit' && (
        <div className="fixed inset-x-0 bottom-0 border-t border-slate-200 bg-white/95 px-6 py-3 backdrop-blur">
          <div className="mx-auto flex max-w-5xl items-center justify-between gap-4">
            <span className="text-sm text-slate-500">
              Estimated total: <strong className="text-slate-900">{currency(estimatedTotal)}</strong>
            </span>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={cancelEdit} disabled={busy}>
                Cancel
              </Button>
              <Button onClick={save} disabled={busy}>
                {busy ? 'Saving…' : 'Save changes'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function PartsSummary({ items }: { items: { id: string; itemName: string; itemQuantity: number; sellingPrice: number; unitProfit: number; itemTotal: number }[] }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Parts &amp; Items</h2>
      {items.length === 0 ? (
        <p className="text-sm text-slate-500">No parts on this job.</p>
      ) : (
        <div className="space-y-3">
          {items.map((i) => (
            <div key={i.id} className="rounded-md border border-slate-200 p-3">
              <p className="font-medium text-slate-900">{i.itemName}</p>
              <div className="mt-2 flex flex-wrap gap-x-6 gap-y-1 text-sm text-slate-600">
                <span>Qty: <strong className="text-slate-900">{i.itemQuantity}</strong></span>
                <span>Selling: <strong className="text-slate-900">{currency(i.sellingPrice)}</strong></span>
                <span>Unit profit: <strong className="text-slate-900">{currency(i.unitProfit)}</strong></span>
                <span>Total: <strong className="text-slate-900">{currency(i.itemTotal)}</strong></span>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

function LabourSummary({ labour }: { labour: { id: string; hours: number | null; ratePerHour: number | null; fixedAmount: number | null; totalAmount: number }[] }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Labour</h2>
      {labour.length === 0 ? (
        <p className="text-sm text-slate-500">No labour on this job.</p>
      ) : (
        <div className="space-y-3">
          {labour.map((l) => (
            <div key={l.id} className="rounded-md border border-slate-200 p-3">
              <div className="flex flex-wrap gap-x-6 gap-y-1 text-sm text-slate-600">
                <span>Hours: <strong className="text-slate-900">{orDash(l.hours)}</strong></span>
                <span>Rate/hr: <strong className="text-slate-900">{currency(l.ratePerHour)}</strong></span>
                <span>Fixed: <strong className="text-slate-900">{currency(l.fixedAmount)}</strong></span>
                <span>Total: <strong className="text-slate-900">{currency(l.totalAmount)}</strong></span>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

function ReadField({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="block text-sm font-medium text-slate-700">{label}</span>
      <p className="mt-1 whitespace-pre-wrap text-sm text-slate-900">{value}</p>
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string | number }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="text-slate-700">{value}</dd>
    </div>
  )
}
