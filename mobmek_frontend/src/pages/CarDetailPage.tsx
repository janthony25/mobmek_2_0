import { Link, useNavigate, useParams } from 'react-router-dom'
import { getCars } from '@/api/cars'
import { getJobs } from '@/api/jobs'
import { Button } from '@/components/ui/Button'
import { RemindersSection } from '@/components/reminders/RemindersSection'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import { currency, orDash } from '@/lib/format'
import { JOB_STATUS_LABELS } from '@/types'

export function CarDetailPage() {
  const { customerId = '', carId = '' } = useParams()
  const navigate = useNavigate()

  const cars = useAsync(() => getCars(customerId), [customerId])
  const jobs = useAsync(() => getJobs(customerId), [customerId])

  if (cars.loading) return <StateMessage title="Loading car…" />
  if (cars.error) return <StateMessage title="Could not load car" description={cars.error.message} />

  const car = cars.data?.find((c) => c.id === carId)
  if (!car) return <StateMessage title="Car not found" />

  const carJobs = (jobs.data ?? []).filter((j) => j.carId === carId)

  return (
    <div className="space-y-8">
      <div>
        <Link to={`/customers/${customerId}`} className="text-sm text-slate-500 hover:underline">
          ← Back to customer
        </Link>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold text-slate-900">
            {car.carMakeName} {car.carModelName} <span className="text-slate-400">· {car.rego}</span>
          </h1>
          <Button onClick={() => navigate('/jobs/new', { state: { customerId, carId } })}>
            + New job for this car
          </Button>
        </div>
        <dl className="mt-3 grid grid-cols-2 gap-x-8 gap-y-1 text-sm text-slate-600 sm:grid-cols-4">
          <Detail label="Year" value={car.year} />
          <Detail label="Rego" value={car.rego} />
          <Detail label="Color" value={orDash(car.color)} />
          <Detail label="Engine" value={orDash(car.engineType)} />
          <Detail label="VIN" value={orDash(car.vin)} />
          <Detail label="Odometer" value={orDash(car.odometer)} />
        </dl>
      </div>

      <section>
        <h2 className="mb-4 text-lg font-semibold text-slate-900">Jobs for this car</h2>
        {jobs.loading && <StateMessage title="Loading jobs…" />}
        {jobs.error && <StateMessage title="Could not load jobs" description={jobs.error.message} />}
        {jobs.data && carJobs.length === 0 && (
          <StateMessage
            title="No jobs for this car yet"
            description="Use “New job for this car” to create one."
          />
        )}
        {carJobs.length > 0 && (
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
                {carJobs.map((job) => (
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

      <RemindersSection
        customerId={customerId}
        lockedCarId={carId}
        description="Reminders for this car, e.g. next WOF or service."
      />
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
