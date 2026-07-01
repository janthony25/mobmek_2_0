import { Link, useParams } from 'react-router-dom'
import { getCustomer } from '@/api/customers'
import { createCar, deleteCar, getCars, updateCar } from '@/api/cars'
import { getJobs } from '@/api/jobs'
import { CrudSection } from '@/components/crud/CrudSection'
import { CarForm } from '@/components/forms/CarForm'
import { RemindersSection } from '@/components/reminders/RemindersSection'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import { currency, orDash } from '@/lib/format'
import { JOB_STATUS_LABELS } from '@/types'
import type { Car, CreateCarRequest, UpdateCarRequest } from '@/types'

export function CustomerDetailPage() {
  const { id = '' } = useParams()
  const { data: customer, loading, error } = useAsync(() => getCustomer(id), [id])

  if (loading) return <StateMessage title="Loading customer…" />
  if (error) return <StateMessage title="Could not load customer" description={error.message} />
  if (!customer) return <StateMessage title="Customer not found" />

  return (
    <div className="space-y-8">
      <div>
        <Link to="/customers" className="text-sm text-slate-500 hover:underline">
          ← Back to customers
        </Link>
        <h1 className="mt-2 text-2xl font-semibold text-slate-900">
          {customer.firstName} {customer.lastName}
        </h1>
        <dl className="mt-3 grid grid-cols-2 gap-x-8 gap-y-1 text-sm text-slate-600 sm:grid-cols-4">
          <Detail label="Phone" value={customer.phoneNumber} />
          <Detail label="Email" value={orDash(customer.emailAddress)} />
          <Detail label="Address" value={orDash(customer.physicalAddress)} />
          <Detail label="Notes" value={orDash(customer.notes)} />
        </dl>
      </div>

      <CrudSection<Car>
        resourceName="Car"
        title="Cars"
        variant="section"
        load={() => getCars(id)}
        getId={(c) => c.id}
        rowLabel={(c) => `${c.carMakeName ?? ''} ${c.carModelName ?? ''} (${c.rego})`}
        columns={[
          {
            header: 'Vehicle',
            cell: (c) => (
              <Link to={`/customers/${id}/cars/${c.id}`} className="font-medium text-slate-900 hover:underline">
                {`${orDash(c.carMakeName)} ${c.carModelName ?? ''}`.trim()}
              </Link>
            ),
          },
          { header: 'Year', cell: (c) => c.year },
          { header: 'Rego', cell: (c) => c.rego },
          { header: 'Color', cell: (c) => orDash(c.color) },
          { header: 'Odometer', cell: (c) => orDash(c.odometer) },
        ]}
        renderForm={(props) => <CarForm {...props} />}
        onCreate={(v) =>
          createCar({ ...(v as Omit<CreateCarRequest, 'customerId'>), customerId: id }).then(() => undefined)
        }
        onUpdate={(carId, v) => updateCar(carId, v as unknown as UpdateCarRequest).then(() => undefined)}
        onDelete={deleteCar}
      />

      <RemindersSection
        customerId={id}
        description="Service, WOF and follow-up reminders for this customer."
      />

      <CustomerJobs customerId={id} />
    </div>
  )
}

function CustomerJobs({ customerId }: { customerId: string }) {
  const { data: jobs, loading, error } = useAsync(() => getJobs(customerId), [customerId])

  return (
    <section>
      <h2 className="mb-1 text-lg font-semibold text-slate-900">All jobs</h2>
      <p className="mb-4 text-sm text-slate-500">Every job for this customer. Open a car above to see just that car's jobs.</p>
      {loading && <StateMessage title="Loading jobs…" />}
      {error && <StateMessage title="Could not load jobs" description={error.message} />}
      {jobs && jobs.length === 0 && (
        <StateMessage title="No jobs" description="Create jobs from the Job Center." />
      )}
      {jobs && jobs.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Title</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {jobs.map((job) => (
                <tr key={job.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <Link to={`/jobs/${job.id}`} className="font-medium text-slate-900 hover:underline">
                      {job.title}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{JOB_STATUS_LABELS[job.status]}</td>
                  <td className="px-4 py-3 text-slate-600">{currency(job.totalJobPrice)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
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
