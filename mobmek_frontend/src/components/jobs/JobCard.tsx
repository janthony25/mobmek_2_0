import { Link } from 'react-router-dom'
import { CarIcon } from '@/components/ui/icons'
import { currency, orDash } from '@/lib/format'
import { JOB_STATUS_COLORS, JOB_STATUS_LABELS } from '@/types'
import type { Job } from '@/types'

interface JobCardProps {
  job: Job
}

export function JobCard({ job }: JobCardProps) {
  return (
    <Link
      to={`/jobs/${job.id}`}
      className="flex h-full flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:shadow-md"
    >
      {/* Header: title + customer, with the status badge on the right. */}
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-semibold text-slate-900">{job.title}</p>
          <p className="text-sm text-slate-500">{orDash(job.customerName)}</p>
        </div>
        <span className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-1 text-xs font-medium ${JOB_STATUS_COLORS[job.status]}`}>
          {JOB_STATUS_LABELS[job.status]}
        </span>
      </div>

      {/* Vehicle */}
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <CarIcon />
        <span>{orDash(job.carDescription)}</span>
      </div>

      {/* Mechanics + total, pinned to the bottom for equal-height cards. */}
      <div className="mt-auto flex items-center justify-between pt-1">
        <div className="flex flex-wrap gap-2">
          {job.mechanics.map((m) => (
            <span key={m.employeeId} className="inline-flex items-center rounded-md bg-slate-100 px-2.5 py-1 text-xs text-slate-700">
              {m.fullName}
            </span>
          ))}
        </div>
        <p className="shrink-0 font-semibold text-slate-900">{currency(job.totalJobPrice)}</p>
      </div>
    </Link>
  )
}
