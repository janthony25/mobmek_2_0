import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { deleteCustomer, getCustomer, updateCustomer } from '@/api/customers'
import { createCar, deleteCar, getCars, updateCar } from '@/api/cars'
import { getJobs } from '@/api/jobs'
import { getInvoices } from '@/api/invoices'
import { getReminders } from '@/api/reminders'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Modal } from '@/components/ui/Modal'
import { StateMessage } from '@/components/ui/StateMessage'
import { useToast } from '@/components/ui/toast'
import {
  BellIcon,
  CarIcon,
  MailIcon,
  MapPinIcon,
  PencilIcon,
  PhoneIcon,
  PlusIcon,
  TrashIcon,
} from '@/components/ui/icons'
import { CarForm } from '@/components/forms/CarForm'
import { ResourceForm } from '@/components/crud/ResourceForm'
import { useAsync } from '@/hooks/useAsync'
import { JOB_STATUS_TONE, invoiceStatusTone } from '@/lib/badges'
import { dueUrgency, URGENCY_BADGE } from '@/lib/dueDate'
import { currency, date, orDash, time } from '@/lib/format'
import { JOB_STATUS_LABELS } from '@/types'
import type { FieldSchema } from '@/components/crud/types'
import type {
  Car,
  CreateCarRequest,
  Customer,
  CustomerRequest,
  Invoice,
  Job,
  Reminder,
  UpdateCarRequest,
} from '@/types'

const customerFields: FieldSchema[] = [
  { name: 'firstName', label: 'First name', type: 'text', required: true },
  { name: 'lastName', label: 'Last name', type: 'text', required: true },
  { name: 'phoneNumber', label: 'Phone number', type: 'text', required: true },
  { name: 'emailAddress', label: 'Email', type: 'text' },
  { name: 'physicalAddress', label: 'Address', type: 'text' },
  { name: 'notes', label: 'Notes', type: 'textarea' },
]

/** First letters of first + last name, e.g. "James Wilson" -> "JW". */
function initials(c: Customer): string {
  return `${c.firstName.charAt(0)}${c.lastName.charAt(0)}`.toUpperCase()
}

export function CustomerDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const toast = useToast()

  const customerState = useAsync(() => getCustomer(id), [id])
  const carsState = useAsync(() => getCars(id), [id])
  const jobsState = useAsync(() => getJobs(id), [id])
  const remindersState = useAsync(() => getReminders({ customerId: id }), [id])
  // Invoices are nested per job, so load the customer's jobs first then their invoices.
  const invoicesState = useAsync(async () => {
    const jobs = jobsState.data ?? []
    if (jobs.length === 0) return []
    const perJob = await Promise.all(jobs.map((job) => getInvoices(job.id)))
    return perJob.flat()
  }, [jobsState.data])

  // Active (not-done) reminders grouped by car, for the per-vehicle badges.
  const remindersByCar = useMemo(() => {
    const map = new Map<string, Reminder[]>()
    for (const reminder of remindersState.data ?? []) {
      if (reminder.isDone || !reminder.carId) continue
      const list = map.get(reminder.carId)
      if (list) list.push(reminder)
      else map.set(reminder.carId, [reminder])
    }
    return map
  }, [remindersState.data])

  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [carModal, setCarModal] = useState<{ open: boolean; car: Car | null }>({ open: false, car: null })
  const [deleteCarTarget, setDeleteCarTarget] = useState<Car | null>(null)

  if (customerState.loading) return <StateMessage title="Loading customer…" />
  if (customerState.error) return <StateMessage title="Could not load customer" description={customerState.error.message} />
  const customer = customerState.data
  if (!customer) return <StateMessage title="Customer not found" />

  const handleUpdateCustomer = async (values: Record<string, unknown>) => {
    await updateCustomer(id, values as unknown as CustomerRequest)
    toast.success('Customer updated')
    setEditOpen(false)
    customerState.reload()
  }

  const handleDeleteCustomer = async () => {
    await deleteCustomer(id)
    toast.success('Customer deleted')
    navigate('/customers')
  }

  const handleSaveCar = async (values: Record<string, unknown>) => {
    if (carModal.car) {
      await updateCar(carModal.car.id, values as unknown as UpdateCarRequest)
      toast.success('Vehicle updated')
    } else {
      await createCar({ ...(values as Omit<CreateCarRequest, 'customerId'>), customerId: id })
      toast.success('Vehicle added')
    }
    setCarModal({ open: false, car: null })
    carsState.reload()
  }

  const handleDeleteCar = async () => {
    if (!deleteCarTarget) return
    await deleteCar(deleteCarTarget.id)
    toast.success('Vehicle deleted')
    setDeleteCarTarget(null)
    carsState.reload()
    remindersState.reload()
  }

  const cars = carsState.data ?? []
  const jobs = [...(jobsState.data ?? [])].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))
  const invoices = [...(invoicesState.data ?? [])].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))

  return (
    <div className="space-y-6">
      <Link to="/customers" className="text-sm text-slate-500 hover:underline">
        ← Customers
      </Link>

      {/* Header: avatar, name, "since", and edit/delete actions. */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-blue-100 text-lg font-bold text-blue-600">
            {initials(customer)}
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">
              {customer.firstName} {customer.lastName}
            </h1>
            <p className="text-sm text-slate-500">Customer since {date(customer.createdAtUtc)}</p>
          </div>
        </div>
        <div className="flex gap-2">
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
        {/* Left column: contact + vehicles */}
        <div className="space-y-6 lg:col-span-1">
          <Card title="Contact">
            <ul className="space-y-3 text-sm text-slate-600">
              <li className="flex items-center gap-3">
                <PhoneIcon className="h-4 w-4 text-slate-400" />
                <span>{customer.phoneNumber}</span>
              </li>
              <li className="flex items-center gap-3">
                <MailIcon className="h-4 w-4 text-slate-400" />
                <span className="truncate">{orDash(customer.emailAddress)}</span>
              </li>
              <li className="flex items-center gap-3">
                <MapPinIcon className="h-4 w-4 text-slate-400" />
                <span>{orDash(customer.physicalAddress)}</span>
              </li>
            </ul>
            {customer.notes && (
              <div className="mt-4 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
                {customer.notes}
              </div>
            )}
          </Card>

          <Card
            title="Vehicles"
            action={
              <Button variant="ghost" size="sm" onClick={() => setCarModal({ open: true, car: null })}>
                <PlusIcon className="h-4 w-4" />
                Add
              </Button>
            }
          >
            {carsState.loading && <p className="text-sm text-slate-500">Loading vehicles…</p>}
            {carsState.error && <p className="text-sm text-red-600">{carsState.error.message}</p>}
            {!carsState.loading && cars.length === 0 && (
              <p className="text-sm text-slate-500">No vehicles yet. Use “Add” to record one.</p>
            )}
            {cars.length > 0 && (
              <div className="divide-y divide-slate-100">
                {cars.map((car) => (
                  <VehicleItem
                    key={car.id}
                    car={car}
                    reminders={remindersByCar.get(car.id) ?? []}
                    onOpen={() => navigate(`/customers/${id}/cars/${car.id}`)}
                    onEdit={() => setCarModal({ open: true, car })}
                    onDelete={() => setDeleteCarTarget(car)}
                  />
                ))}
              </div>
            )}
          </Card>
        </div>

        {/* Right column: appointment history + invoices */}
        <div className="space-y-6 lg:col-span-2">
          <Card title="Appointment History">
            {jobsState.loading && <p className="text-sm text-slate-500">Loading appointments…</p>}
            {jobsState.error && <p className="text-sm text-red-600">{jobsState.error.message}</p>}
            {!jobsState.loading && jobs.length === 0 && (
              <p className="text-sm text-slate-500">No jobs yet. Create one from the Job Center.</p>
            )}
            {jobs.length > 0 && (
              <div className="divide-y divide-slate-100">
                {jobs.map((job) => (
                  <AppointmentRow key={job.id} job={job} />
                ))}
              </div>
            )}
          </Card>

          <Card title="Invoices">
            {invoicesState.loading && <p className="text-sm text-slate-500">Loading invoices…</p>}
            {invoicesState.error && <p className="text-sm text-red-600">{invoicesState.error.message}</p>}
            {!invoicesState.loading && invoices.length === 0 && (
              <p className="text-sm text-slate-500">No invoices yet.</p>
            )}
            {invoices.length > 0 && (
              <div className="divide-y divide-slate-100">
                {invoices.map((invoice) => (
                  <InvoiceRow key={invoice.id} invoice={invoice} />
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      <Modal open={editOpen} title="Edit customer" onClose={() => setEditOpen(false)}>
        <ResourceForm
          fields={customerFields}
          initial={customer as unknown as Record<string, unknown>}
          onSubmit={handleUpdateCustomer}
          onCancel={() => setEditOpen(false)}
        />
      </Modal>

      <Modal
        open={carModal.open}
        title={carModal.car ? 'Edit vehicle' : 'Add vehicle'}
        onClose={() => setCarModal({ open: false, car: null })}
      >
        <CarForm
          initial={carModal.car}
          onSubmit={handleSaveCar}
          onCancel={() => setCarModal({ open: false, car: null })}
        />
      </Modal>

      <ConfirmDialog
        open={deleteOpen}
        title="Delete customer"
        message={`Delete ${customer.firstName} ${customer.lastName}? This cannot be undone.`}
        onConfirm={handleDeleteCustomer}
        onCancel={() => setDeleteOpen(false)}
      />

      <ConfirmDialog
        open={deleteCarTarget !== null}
        title="Delete vehicle"
        message={
          deleteCarTarget
            ? `Delete ${deleteCarTarget.year} ${deleteCarTarget.carMakeName ?? ''} ${deleteCarTarget.carModelName ?? ''} (${deleteCarTarget.rego})?`
            : ''
        }
        onConfirm={handleDeleteCar}
        onCancel={() => setDeleteCarTarget(null)}
      />
    </div>
  )
}

interface VehicleItemProps {
  car: Car
  reminders: Reminder[]
  onOpen: () => void
  onEdit: () => void
  onDelete: () => void
}

function VehicleItem({ car, reminders, onOpen, onEdit, onDelete }: VehicleItemProps) {
  const urgency = dueUrgency(reminders.map((r) => r.dueDate))
  const subtitle = [car.color, car.rego, car.odometer != null ? `${car.odometer.toLocaleString()} km` : null]
    .filter(Boolean)
    .join(' · ')

  return (
    <div
      onClick={onOpen}
      className="flex cursor-pointer items-start gap-3 py-3 first:pt-0 last:pb-0"
    >
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-500">
        <CarIcon className="h-5 w-5" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="font-medium text-slate-900">
          {car.year} {car.carMakeName} {car.carModelName}
        </p>
        <p className="truncate text-sm text-slate-500">{subtitle}</p>
        <p className="mt-1 font-mono text-xs text-slate-400">VIN: {orDash(car.vin)}</p>
      </div>
      <div className="flex items-center gap-1" onClick={(e) => e.stopPropagation()}>
        {reminders.length > 0 && (
          <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${URGENCY_BADGE[urgency]}`}>
            <BellIcon className="h-3.5 w-3.5" />
            {reminders.length}
          </span>
        )}
        <button
          type="button"
          onClick={onEdit}
          aria-label="Edit vehicle"
          className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
        >
          <PencilIcon className="h-4 w-4" />
        </button>
        <button
          type="button"
          onClick={onDelete}
          aria-label="Delete vehicle"
          className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-red-600"
        >
          <TrashIcon className="h-4 w-4" />
        </button>
      </div>
    </div>
  )
}

function AppointmentRow({ job }: { job: Job }) {
  const mechanic = job.mechanics[0]?.fullName
  const details = [job.carDescription, mechanic].filter(Boolean).join(' · ')

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
        <p className="truncate text-sm text-slate-500">{orDash(details)}</p>
      </div>
      <Badge tone={JOB_STATUS_TONE[job.status]}>{JOB_STATUS_LABELS[job.status]}</Badge>
    </div>
  )
}

function InvoiceRow({ invoice }: { invoice: Invoice }) {
  return (
    <div className="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0">
      <div className="min-w-0">
        <p className="font-medium text-slate-900">{invoice.issueName}</p>
        <p className="text-sm text-slate-500">
          Issued {date(invoice.createdAtUtc)} · Due {date(invoice.dueDate)}
        </p>
      </div>
      <div className="flex items-center gap-3">
        <span className="font-semibold text-slate-900">{currency(invoice.totalAmount)}</span>
        <Badge tone={invoiceStatusTone(invoice.status)}>{invoice.status}</Badge>
      </div>
    </div>
  )
}
