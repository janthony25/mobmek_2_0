import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { deleteCar, getCars, updateCar } from '@/api/cars'
import { getJobs } from '@/api/jobs'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Modal } from '@/components/ui/Modal'
import { PaginatedList } from '@/components/ui/PaginatedList'
import { Spinner } from '@/components/ui/Spinner'
import { StateMessage } from '@/components/ui/StateMessage'
import { UpdatedByTag } from '@/components/ui/UpdatedByTag'
import { useToast } from '@/components/ui/toast'
import { CarIcon, PencilIcon, PlusIcon, TrashIcon } from '@/components/ui/icons'
import { CarForm } from '@/components/forms/CarForm'
import { RemindersSection } from '@/components/reminders/RemindersSection'
import { useAsync } from '@/hooks/useAsync'
import { JOB_STATUS_TONE } from '@/lib/badges'
import { date, orDash, time } from '@/lib/format'
import { JOB_STATUS_LABELS } from '@/types'
import type { Job, UpdateCarRequest } from '@/types'

export function CarDetailPage() {
  const { customerId = '', carId = '' } = useParams()
  const navigate = useNavigate()
  const toast = useToast()

  const carsState = useAsync(() => getCars(customerId), [customerId])
  const jobsState = useAsync(() => getJobs(customerId), [customerId])

  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  const car = carsState.data?.find((c) => c.id === carId)

  if (carsState.loading && !car) return <StateMessage title="Loading car…" loading />
  if (carsState.error) return <StateMessage title="Could not load car" description={carsState.error.message} />
  if (!car) return <StateMessage title="Car not found" />

  const carJobs = [...(jobsState.data ?? [])]
    .filter((j) => j.carId === carId)
    .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))

  const subtitle = [car.color, car.rego].filter(Boolean).join(' · ')

  const handleUpdateCar = async (values: Record<string, unknown>) => {
    await updateCar(carId, values as unknown as UpdateCarRequest)
    toast.success('Vehicle updated')
    setEditOpen(false)
    carsState.reload()
  }

  const handleDeleteCar = async () => {
    await deleteCar(carId)
    toast.success('Vehicle deleted')
    navigate(`/customers/${customerId}`)
  }

  return (
    <div className="space-y-6">
      <Link to={`/customers/${customerId}`} className="text-sm text-slate-500 hover:underline">
        ← Back to customer
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-slate-100 text-slate-500">
            <CarIcon className="h-7 w-7" />
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">
              {car.year} {car.carMakeName} {car.carModelName}
            </h1>
            <p className="text-sm text-slate-500">{subtitle}</p>
            <UpdatedByTag className="mt-0.5 block" updatedAtUtc={car.updatedAtUtc} updatedByName={car.updatedByName} />
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => navigate('/jobs/new', { state: { customerId, carId } })}>
            <PlusIcon className="h-4 w-4" />
            New job
          </Button>
          <Button variant="secondary" onClick={() => setEditOpen(true)}>
            <PencilIcon className="h-4 w-4" />
            Edit
          </Button>
          <Button variant="danger" onClick={() => setDeleteOpen(true)}>
            <TrashIcon className="h-4 w-4" />
            Delete
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-1">
          <Card title="Details">
            <dl className="grid grid-cols-2 gap-x-6 gap-y-4 text-sm">
              <Detail label="Year" value={car.year} />
              <Detail label="Rego" value={car.rego} />
              <Detail label="Color" value={orDash(car.color)} />
              <Detail label="Engine" value={orDash(car.engineType)} />
              <Detail label="VIN" value={orDash(car.vin)} mono />
            </dl>
          </Card>
        </div>

        <div className="lg:col-span-2">
          <RemindersSection
            customerId={customerId}
            lockedCarId={carId}
            description="Reminders for this car, e.g. next WOF or service."
          />
        </div>
      </div>

      <Card title="Jobs">
        {jobsState.loading && (
          <p className="flex items-center gap-2 text-sm text-slate-500">
            <Spinner className="h-3.5 w-3.5" /> Loading jobs…
          </p>
        )}
        {jobsState.error && <p className="text-sm text-red-600">{jobsState.error.message}</p>}
        {!jobsState.loading && carJobs.length === 0 && (
          <p className="text-sm text-slate-500">No jobs for this car yet. Use “New job” to create one.</p>
        )}
        {carJobs.length > 0 && (
          <PaginatedList
            items={carJobs}
            pageSize={15}
            getKey={(job) => job.id}
            renderItem={(job) => <AppointmentRow job={job} />}
          />
        )}
      </Card>

      <Modal open={editOpen} title="Edit vehicle" onClose={() => setEditOpen(false)}>
        <CarForm initial={car} onSubmit={handleUpdateCar} onCancel={() => setEditOpen(false)} />
      </Modal>

      <ConfirmDialog
        open={deleteOpen}
        title="Delete vehicle"
        message={`Delete ${car.year} ${car.carMakeName ?? ''} ${car.carModelName ?? ''} (${car.rego})?`}
        onConfirm={handleDeleteCar}
        onCancel={() => setDeleteOpen(false)}
      />
    </div>
  )
}

function AppointmentRow({ job }: { job: Job }) {
  const mechanic = job.mechanics[0]?.fullName

  return (
    <div className="flex items-center gap-4 py-3 first:pt-0 last:pb-0">
      <div className="w-24 shrink-0">
        <p className="text-sm font-medium text-slate-900">{date(job.createdAtUtc)}</p>
        <p className="text-xs text-slate-500">{time(job.createdAtUtc)}</p>
      </div>
      <div className="min-w-0 flex-1">
        <Link to={`/jobs/${job.id}`} className="block truncate font-medium text-slate-900 hover:underline">
          {job.title}
        </Link>
        <p className="truncate text-sm text-slate-500">{orDash(mechanic)}</p>
      </div>
      <Badge tone={JOB_STATUS_TONE[job.status]}>{JOB_STATUS_LABELS[job.status]}</Badge>
    </div>
  )
}

function Detail({ label, value, mono }: { label: string; value: string | number; mono?: boolean }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className={`text-slate-700 ${mono ? 'font-mono text-xs' : ''}`}>{value}</dd>
    </div>
  )
}
