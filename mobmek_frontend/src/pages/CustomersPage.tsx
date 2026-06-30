import { getCustomers } from '@/api/customers'
import { PageHeader } from '@/components/ui/PageHeader'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'

export function CustomersPage() {
  const { data: customers, loading, error } = useAsync(getCustomers, [])

  return (
    <div>
      <PageHeader title="Customers" description="Everyone with a record in the workshop." />

      {loading && <StateMessage title="Loading customers…" />}
      {error && <StateMessage title="Could not load customers" description={error.message} />}
      {customers && customers.length === 0 && (
        <StateMessage title="No customers yet" description="Customers will appear here once added." />
      )}

      {customers && customers.length > 0 && (
        <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">Phone</th>
                <th className="px-4 py-3">Email</th>
                <th className="px-4 py-3">Address</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {customers.map((customer) => (
                <tr key={customer.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-medium text-slate-900">
                    {customer.firstName} {customer.lastName}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{customer.phoneNumber}</td>
                  <td className="px-4 py-3 text-slate-600">{customer.emailAddress ?? '—'}</td>
                  <td className="px-4 py-3 text-slate-600">{customer.physicalAddress ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
