import { Link, useNavigate } from 'react-router-dom'
import { createJob, deleteJob, getJobsPaged, updateJob } from '@/api/jobs'
import { CrudSection } from '@/components/crud/CrudSection'
import { JobForm } from '@/components/forms/JobForm'
import { JobCard } from '@/components/jobs/JobCard'
import { currency, orDash } from '@/lib/format'
import { JOB_STATUS_LABELS } from '@/types'
import type { CreateJobRequest, Job, UpdateJobRequest } from '@/types'

export function JobCenterPage() {
  const navigate = useNavigate()
  return (
    <CrudSection<Job>
      resourceName="Job"
      title="Job Center"
      description="Workshop jobs across all customers. Open a job to manage items, labour, services and mechanics."
      onAdd={() => navigate('/jobs/new')}
      loadPaged={({ page, pageSize, search }) => getJobsPaged(page, pageSize, search)}
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
  )
}
