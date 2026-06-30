import { createJobService, deleteJobService, getJobServices, updateJobService } from '@/api/jobServices'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import { currency, orDash } from '@/lib/format'
import type { JobService, JobServiceRequest } from '@/types'

const fields: FieldSchema[] = [
  { name: 'name', label: 'Name', type: 'text', required: true, placeholder: 'e.g. Oil change' },
  { name: 'price', label: 'Price', type: 'number', required: true, step: '0.01', min: 0 },
  { name: 'description', label: 'Description', type: 'textarea' },
  { name: 'isActive', label: 'Active', type: 'checkbox', defaultValue: true },
]

export function JobServicesPage() {
  return (
    <CrudSection<JobService>
      resourceName="Service"
      title="Catalog Services"
      description="Reusable services that can be added to jobs."
      load={() => getJobServices()}
      getId={(s) => s.id}
      rowLabel={(s) => s.name}
      columns={[
        { header: 'Name', cell: (s) => s.name, className: 'font-medium text-slate-900' },
        { header: 'Price', cell: (s) => currency(s.price) },
        { header: 'Active', cell: (s) => (s.isActive ? 'Yes' : 'No') },
        { header: 'Description', cell: (s) => orDash(s.description) },
      ]}
      fields={fields}
      onCreate={(v) => createJobService(v as unknown as JobServiceRequest).then(() => undefined)}
      onUpdate={(id, v) => updateJobService(id, v as unknown as JobServiceRequest).then(() => undefined)}
      onDelete={deleteJobService}
    />
  )
}
