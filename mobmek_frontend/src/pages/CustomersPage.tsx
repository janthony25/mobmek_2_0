import { Link } from 'react-router-dom'
import { createCustomer, deleteCustomer, getCustomers, updateCustomer } from '@/api/customers'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import { orDash } from '@/lib/format'
import type { Customer, CustomerRequest } from '@/types'

const fields: FieldSchema[] = [
  { name: 'firstName', label: 'First name', type: 'text', required: true },
  { name: 'lastName', label: 'Last name', type: 'text', required: true },
  { name: 'phoneNumber', label: 'Phone number', type: 'text', required: true },
  { name: 'emailAddress', label: 'Email', type: 'text' },
  { name: 'physicalAddress', label: 'Address', type: 'text' },
  { name: 'notes', label: 'Notes', type: 'textarea' },
]

export function CustomersPage() {
  return (
    <CrudSection<Customer>
      resourceName="Customer"
      description="Everyone with a record in the workshop. Open a customer to manage their cars and jobs."
      load={getCustomers}
      getId={(c) => c.id}
      rowLabel={(c) => `${c.firstName} ${c.lastName}`}
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
