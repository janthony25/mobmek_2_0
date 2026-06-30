import { getJobs } from '@/api/jobs'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import { JOB_STATUS_LABELS, JobStatus } from '@/types'

const STATUS_STYLES: Record<JobStatus, string> = {
  [JobStatus.Open]: 'bg-blue-100 text-blue-700',
  [JobStatus.InProgress]: 'bg-amber-100 text-amber-700',
  [JobStatus.AwaitingParts]: 'bg-purple-100 text-purple-700',
  [JobStatus.Completed]: 'bg-green-100 text-green-700',
  [JobStatus.Invoiced]: 'bg-slate-200 text-slate-700',
}

const currency = new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' })

export function JobCenterPage() {
  const { data: jobs, loading, error } = useAsync(() => getJobs(), [])

  return (
    <div>
      <PageHeader title="Job Center" description="Workshop jobs across all customers." />

      {loading && <StateMessage title="Loading jobs…" />}
      {error && <StateMessage title="Could not load jobs" description={error.message} />}
      {jobs && jobs.length === 0 && (
        <StateMessage title="No jobs yet" description="Created jobs will show up here." />
      )}

      {jobs && jobs.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2">
          {jobs.map((job) => (
            <article
              key={job.id}
              className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
            >
              <div className="flex items-start justify-between gap-3">
                <h2 className="font-semibold text-slate-900">{job.title}</h2>
                <span
                  className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_STYLES[job.status]}`}
                >
                  {JOB_STATUS_LABELS[job.status]}
                </span>
              </div>

              <dl className="mt-3 space-y-1 text-sm text-slate-600">
                <div className="flex justify-between">
                  <dt className="text-slate-400">Customer</dt>
                  <dd>{job.customerName ?? '—'}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-slate-400">Vehicle</dt>
                  <dd>{job.carDescription ?? '—'}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-slate-400">Odometer</dt>
                  <dd>{job.odometer.toLocaleString()} km</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-slate-400">Total</dt>
                  <dd className="font-medium text-slate-900">{currency.format(job.totalJobPrice)}</dd>
                </div>
              </dl>

              {job.mechanics.length > 0 && (
                <p className="mt-3 text-xs text-slate-500">
                  Mechanics: {job.mechanics.map((m) => m.fullName).join(', ')}
                </p>
              )}
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
