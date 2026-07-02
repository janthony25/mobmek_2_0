import { Link } from 'react-router-dom'
import { createCustomer, deleteCustomer, getCustomersPaged, updateCustomer } from '@/api/customers'
import { CrudSection } from '@/components/crud/CrudSection'
import { CustomerCard } from '@/components/customers/CustomerCard'
import type { FieldSchema } from '@/components/crud/types'
import { orDash } from '@/lib/format'
import type { CustomerListItem, CustomerRequest } from '@/types'

const fields: FieldSchema[] = [
  { name: 'firstName', label: 'First name', type: 'text', required: true },
  { name: 'lastName', label: 'Last name', type: 'text', required: true },
  { name: 'phoneNumber', label: 'Phone number', type: 'text', required: true },
  { name: 'emailAddress', label: 'Email', type: 'text' },
  { name: 'physicalAddress', label: 'Address', type: 'text' },
  { name: 'notes', label: 'Notes', type: 'textarea' },
]

export function CustomersPage() {
  // Server-side pagination + search; each list item carries the car/note/reminder
  // aggregates the cards display, so no extra collection fetches are needed.
  return (
    <CrudSection<CustomerListItem>
      resourceName="Customer"
      description="Everyone with a record in the workshop. Open a customer to manage their cars and jobs."
      loadPaged={({ page, pageSize, search }) => getCustomersPaged(page, pageSize, search)}
      pageSize={20}
      cardsPageSize={10}
      getId={(c) => c.id}
      rowLabel={(c) => `${c.firstName} ${c.lastName}`}
      defaultView="cards"
      renderCard={(c) => <CustomerCard customer={c} />}
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
