import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { createCustomer, deleteCustomer, getCustomers, updateCustomer } from '@/api/customers'
import { getCars } from '@/api/cars'
import { getNotes } from '@/api/notes'
import { getReminders } from '@/api/reminders'
import { CrudSection } from '@/components/crud/CrudSection'
import { CustomerCard } from '@/components/customers/CustomerCard'
import { useAsync } from '@/hooks/useAsync'
import type { FieldSchema } from '@/components/crud/types'
import { orDash } from '@/lib/format'
import type { Car, Customer, CustomerRequest, Note, Reminder } from '@/types'

const fields: FieldSchema[] = [
  { name: 'firstName', label: 'First name', type: 'text', required: true },
  { name: 'lastName', label: 'Last name', type: 'text', required: true },
  { name: 'phoneNumber', label: 'Phone number', type: 'text', required: true },
  { name: 'emailAddress', label: 'Email', type: 'text' },
  { name: 'physicalAddress', label: 'Address', type: 'text' },
  { name: 'notes', label: 'Notes', type: 'textarea' },
]

export function CustomersPage() {
  // The card view shows each customer's vehicles, plus note/reminder counts; fetch each
  // collection once and group by customer rather than making a request per card.
  const { data: cars } = useAsync(() => getCars(), [])
  const { data: notes } = useAsync(() => getNotes(), [])
  const { data: reminders } = useAsync(() => getReminders(), [])

  const carsByCustomer = useMemo(() => {
    const map = new Map<string, Car[]>()
    for (const car of cars ?? []) {
      const list = map.get(car.customerId)
      if (list) list.push(car)
      else map.set(car.customerId, [car])
    }
    return map
  }, [cars])

  const notesByCustomer = useMemo(() => {
    const map = new Map<string, Note[]>()
    for (const note of notes ?? []) {
      if (note.isDone || !note.customerId) continue
      const list = map.get(note.customerId)
      if (list) list.push(note)
      else map.set(note.customerId, [note])
    }
    return map
  }, [notes])

  const remindersByCustomer = useMemo(() => {
    const map = new Map<string, Reminder[]>()
    for (const reminder of reminders ?? []) {
      if (reminder.isDone) continue
      const list = map.get(reminder.customerId)
      if (list) list.push(reminder)
      else map.set(reminder.customerId, [reminder])
    }
    return map
  }, [reminders])

  return (
    <CrudSection<Customer>
      resourceName="Customer"
      description="Everyone with a record in the workshop. Open a customer to manage their cars and jobs."
      load={getCustomers}
      getId={(c) => c.id}
      rowLabel={(c) => `${c.firstName} ${c.lastName}`}
      defaultView="cards"
      renderCard={(c) => (
        <CustomerCard
          customer={c}
          cars={carsByCustomer.get(c.id) ?? []}
          notes={notesByCustomer.get(c.id) ?? []}
          reminders={remindersByCustomer.get(c.id) ?? []}
        />
      )}
      columns={[
        {
          header: 'Name',
          cell: (c) => (
            <Link to={`/customers/${c.id}`} className="font-medium text-slate-900 hover:underline">
              {c.firstName} {c.lastName}
            </Link>
          ),
        },
        { header: 'Phone', cell: (c) => c.phoneNumber },
        { header: 'Email', cell: (c) => orDash(c.emailAddress) },
        { header: 'Address', cell: (c) => orDash(c.physicalAddress) },
      ]}
      fields={fields}
      onCreate={(v) => createCustomer(v as unknown as CustomerRequest).then(() => undefined)}
      onUpdate={(id, v) => updateCustomer(id, v as unknown as CustomerRequest).then(() => undefined)}
      onDelete={deleteCustomer}
    />
  )
}
