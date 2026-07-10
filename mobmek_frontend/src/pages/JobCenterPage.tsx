import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { createJob, deleteJob, getJobsPaged, updateJob } from '@/api/jobs'
import type { JobPagedFilters } from '@/api/jobs'
import { CrudSection } from '@/components/crud/CrudSection'
import { DateRangeFilter } from '@/components/ui/DateRangeFilter'
import { JobForm } from '@/components/forms/JobForm'
import { JobCard } from '@/components/jobs/JobCard'
import { currency, orDash } from '@/lib/format'
import { JOB_STATUS_LABELS, JobStatus } from '@/types'
import type { CreateJobRequest, Job, UpdateJobRequest } from '@/types'

export function JobCenterPage() {
  const navigate = useNavigate()
  const [sortBy, setSortBy] = useState<NonNullable<JobPagedFilters['sortBy']>>('newest')
  const [status, setStatus] = useState<JobStatus | ''>('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <select
          value={sortBy}
          onChange={(e) => setSortBy(e.target.value as NonNullable<JobPagedFilters['sortBy']>)}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700 focus:border-slate-500 focus:outline-none"
        >
          <option value="newest">Newest first</option>
          <option value="oldest">Oldest first</option>
        </select>
        <select
          value={status}
          onChange={(e) => setStatus(e.target.value === '' ? '' : (Number(e.target.value) as JobStatus))}
          className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700 focus:border-slate-500 focus:outline-none"
        >
          <option value="">All statuses</option>
          {Object.entries(JOB_STATUS_LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <DateRangeFilter dateFrom={dateFrom} dateTo={dateTo} onDateFromChange={setDateFrom} onDateToChange={setDateTo} />
      </div>

      <CrudSection<Job>
        resourceName="Job"
        title="Job Center"
        description="Workshop jobs across all customers. Open a job to manage items, labour, services and mechanics."
        onAdd={() => navigate('/jobs/new')}
        loadPaged={({ page, pageSize, search }) =>
          getJobsPaged(page, pageSize, search, {
            sortBy,
            status: status === '' ? undefined : status,
            dateFrom: dateFrom || undefined,
            dateTo: dateTo || undefined,
          })
        }
        reloadKey={`${sortBy}-${status}-${dateFrom}-${dateTo}`}
        pageSize={15}
        cardsPageSize={10}
        getId={(j) => j.id}
        rowLabel={(j) => j.title}
        defaultView="cards"
        renderCard={(j) => <JobCard job={j} />}
        columns={[
          {
            header: 'Title',
            cell: (j) => (
              <Link to={`/jobs/${j.id}`} className="font-medium text-slate-900 hover:underline">
                {j.title}
              </Link>
            ),
          },
          { header: 'Customer', cell: (j) => orDash(j.customerName) },
          { header: 'Vehicle', cell: (j) => orDash(j.carDescription) },
          { header: 'Status', cell: (j) => JOB_STATUS_LABELS[j.status] },
          { header: 'Total', cell: (j) => currency(j.totalJobPrice) },
        ]}
        renderForm={(props) => <JobForm {...props} />}
        onCreate={(v) => createJob(v as unknown as CreateJobRequest).then(() => undefined)}
        onUpdate={(id, v) => updateJob(id, v as unknown as UpdateJobRequest).then(() => undefined)}
        onDelete={deleteJob}
      />
    </div>
  )
}
