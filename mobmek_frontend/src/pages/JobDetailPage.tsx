import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { addJobMechanic, getJob, removeJobMechanic } from '@/api/jobs'
import { createJobItem, deleteJobItem, getJobItems, updateJobItem } from '@/api/jobItems'
import { createLabour, deleteLabour, getLabour, updateLabour } from '@/api/labour'
import {
  createJobServiceLine,
  deleteJobServiceLine,
  getJobServiceLines,
  updateJobServiceLine,
} from '@/api/jobServiceLines'
import { getJobServices } from '@/api/jobServices'
import { getEmployees } from '@/api/employees'
import { CrudSection } from '@/components/crud/CrudSection'
import { InvoicesSection } from '@/components/jobs/InvoicesSection'
import { Button } from '@/components/ui/Button'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import type { FieldSchema } from '@/components/crud/types'
import { useAsync } from '@/hooks/useAsync'
import { currency, orDash } from '@/lib/format'
import {
  JOB_STATUS_LABELS,
  MARKUP_SOLUTION_LABELS,
  type Job,
  type JobItem,
  type JobItemRequest,
  type JobServiceLine,
  type Labour,
  type LabourRequest,
} from '@/types'

export function JobDetailPage() {
  const { id = '' } = useParams()
  const { data: job, loading, error, reload } = useAsync(() => getJob(id), [id])
  const services = useAsync(() => getJobServices(), [])
  const employees = useAsync(getEmployees, [])

  if (loading) return <StateMessage title="Loading job…" />
  if (error) return <StateMessage title="Could not load job" description={error.message} />
  if (!job) return <StateMessage title="Job not found" />

  return (
    <div className="space-y-8">
      <div>
        <Link to="/jobs" className="text-sm text-slate-500 hover:underline">
          ← Back to Job Center
        </Link>
        <h1 className="mt-2 text-2xl font-semibold text-slate-900">{job.title}</h1>
        <dl className="mt-3 grid grid-cols-2 gap-x-8 gap-y-1 text-sm text-slate-600 sm:grid-cols-4">
          <Detail label="Customer" value={orDash(job.customerName)} />
          <Detail label="Vehicle" value={orDash(job.carDescription)} />
          <Detail label="Status" value={JOB_STATUS_LABELS[job.status]} />
          <Detail label="Odometer" value={job.odometer.toLocaleString()} />
          <Detail label="Total price" value={currency(job.totalJobPrice)} />
          <Detail label="Total profit" value={currency(job.totalJobProfit)} />
        </dl>
      </div>

      <MechanicsSection job={job} employees={employees.data ?? []} onChanged={reload} />

      <CrudSection<JobItem>
        resourceName="Item"
        title="Parts & Items"
        variant="section"
        load={() => getJobItems(id)}
        getId={(i) => i.id}
        rowLabel={(i) => i.itemName}
        columns={[
          { header: 'Item', cell: (i) => i.itemName, className: 'font-medium text-slate-900' },
          { header: 'Qty', cell: (i) => i.itemQuantity },
          { header: 'Selling', cell: (i) => currency(i.sellingPrice) },
          { header: 'Unit profit', cell: (i) => currency(i.unitProfit) },
          { header: 'Total', cell: (i) => currency(i.itemTotal) },
        ]}
        fields={jobItemFields}
        onCreate={(v) => createJobItem(id, v as unknown as JobItemRequest).then(() => undefined)}
        onUpdate={(itemId, v) => updateJobItem(id, itemId, v as unknown as JobItemRequest).then(() => undefined)}
        onDelete={(itemId) => deleteJobItem(id, itemId)}
        onChanged={reload}
      />

      <CrudSection<Labour>
        resourceName="Labour"
        title="Labour"
        variant="section"
        load={() => getLabour(id)}
        getId={(l) => l.id}
        rowLabel={() => 'labour line'}
        columns={[
          { header: 'Hours', cell: (l) => orDash(l.hours) },
          { header: 'Rate/hr', cell: (l) => currency(l.ratePerHour) },
          { header: 'Fixed', cell: (l) => currency(l.fixedAmount) },
          { header: 'Total', cell: (l) => currency(l.totalAmount), className: 'font-medium text-slate-900' },
        ]}
        fields={labourFields}
        onCreate={(v) => createLabour(id, v as unknown as LabourRequest).then(() => undefined)}
        onUpdate={(labourId, v) => updateLabour(id, labourId, v as unknown as LabourRequest).then(() => undefined)}
        onDelete={(labourId) => deleteLabour(id, labourId)}
        onChanged={reload}
      />

      <CrudSection<JobServiceLine>
        resourceName="Service"
        title="Services"
        variant="section"
        reloadKey={services.data?.length}
        load={() => getJobServiceLines(id)}
        getId={(s) => s.id}
        rowLabel={(s) => s.serviceName ?? 'service'}
        columns={[
          { header: 'Service', cell: (s) => orDash(s.serviceName), className: 'font-medium text-slate-900' },
          { header: 'Unit price', cell: (s) => currency(s.unitPrice) },
          { header: 'Qty', cell: (s) => s.quantity },
          { header: 'Total', cell: (s) => currency(s.lineTotal) },
        ]}
        fields={[
          {
            name: 'jobServiceId',
            label: 'Service',
            type: 'select',
            required: true,
            options: (services.data ?? []).map((s) => ({ value: s.id, label: `${s.name} (${currency(s.price)})` })),
          },
          { name: 'quantity', label: 'Quantity', type: 'number', required: true, min: 1, defaultValue: 1 },
        ]}
        onCreate={(v) =>
          createJobServiceLine(id, {
            jobServiceId: v.jobServiceId as string,
            quantity: Number(v.quantity),
          }).then(() => undefined)
        }
        onUpdate={(lineId, v) => updateJobServiceLine(id, lineId, Number(v.quantity)).then(() => undefined)}
        onDelete={(lineId) => deleteJobServiceLine(id, lineId)}
        onChanged={reload}
      />

      <InvoicesSection jobId={id} />
    </div>
  )
}

const jobItemFields: FieldSchema[] = [
  { name: 'itemName', label: 'Item name', type: 'text', required: true },
  { name: 'itemQuantity', label: 'Quantity', type: 'number', required: true, min: 1, defaultValue: 1 },
  { name: 'tradePrice', label: 'Trade price', type: 'number', step: '0.01', min: 0, help: 'If set, selling price is derived via markup.' },
  {
    name: 'markupSolution',
    label: 'Markup type',
    type: 'select',
    numeric: true,
    defaultValue: 0,
    options: Object.entries(MARKUP_SOLUTION_LABELS).map(([value, label]) => ({ value, label })),
  },
  { name: 'markup', label: 'Markup', type: 'number', step: '0.01', min: 0, defaultValue: 0 },
  { name: 'retailPrice', label: 'Retail price', type: 'number', step: '0.01', min: 0 },
  { name: 'sellingPrice', label: 'Selling price', type: 'number', step: '0.01', min: 0, help: 'Used when no trade price is given.' },
]

const labourFields: FieldSchema[] = [
  { name: 'hours', label: 'Hours', type: 'number', step: '0.1', min: 0 },
  { name: 'ratePerHour', label: 'Rate per hour', type: 'number', step: '0.01', min: 0 },
  { name: 'fixedAmount', label: 'Fixed amount', type: 'number', step: '0.01', min: 0, help: 'If set, overrides hours × rate.' },
]

function MechanicsSection({
  job,
  employees,
  onChanged,
}: {
  job: Job
  employees: { id: string; firstName: string; lastName: string }[]
  onChanged: () => void
}) {
  const toast = useToast()
  const [employeeId, setEmployeeId] = useState('')
  const [busy, setBusy] = useState(false)

  const assignedIds = new Set(job.mechanics.map((m) => m.employeeId))
  const available = employees.filter((e) => !assignedIds.has(e.id))

  const add = async () => {
    if (!employeeId) return
    setBusy(true)
    try {
      await addJobMechanic(job.id, employeeId)
      toast.success('Mechanic assigned')
      setEmployeeId('')
      onChanged()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  const remove = async (id: string) => {
    setBusy(true)
    try {
      await removeJobMechanic(job.id, id)
      toast.success('Mechanic removed')
      onChanged()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : String(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section>
      <h2 className="mb-4 text-lg font-semibold text-slate-900">Mechanics</h2>
      <div className="rounded-lg border border-slate-200 bg-white p-4">
        {job.mechanics.length === 0 ? (
          <p className="text-sm text-slate-500">No mechanics assigned.</p>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {job.mechanics.map((m) => (
              <li
                key={m.employeeId}
                className="inline-flex items-center gap-2 rounded-full bg-slate-100 py-1 pl-3 pr-1 text-sm text-slate-700"
              >
                {m.fullName}
                <button
                  type="button"
                  onClick={() => remove(m.employeeId)}
                  disabled={busy}
                  className="rounded-full px-1.5 text-slate-400 hover:bg-slate-200 hover:text-slate-700"
                  aria-label={`Remove ${m.fullName}`}
                >
                  ✕
                </button>
              </li>
            ))}
          </ul>
        )}

        <div className="mt-4 flex items-center gap-2">
          <select
            value={employeeId}
            onChange={(e) => setEmployeeId(e.target.value)}
            className="rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
          >
            <option value="">Assign a mechanic…</option>
            {available.map((e) => (
              <option key={e.id} value={e.id}>
                {e.firstName} {e.lastName}
              </option>
            ))}
          </select>
          <Button onClick={add} disabled={busy || !employeeId} size="sm">
            Assign
          </Button>
        </div>
      </div>
    </section>
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
