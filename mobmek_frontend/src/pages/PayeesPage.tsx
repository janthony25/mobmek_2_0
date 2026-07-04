import { createPayee, deletePayee, getPayees, updatePayee } from '@/api/payees'
import { getTransactionCategories } from '@/api/transactionCategories'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import { StateMessage } from '@/components/ui/StateMessage'
import { useAsync } from '@/hooks/useAsync'
import { orDash } from '@/lib/format'
import type { Payee, PayeeRequest } from '@/types'

const toRequest = (v: Record<string, unknown>): PayeeRequest => ({
  name: v.name as string,
  defaultCategoryId: (v.defaultCategoryId as string) || null,
  defaultGstTreatment: (v.defaultGstTreatment as string) || null,
  notes: (v.notes as string) || null,
  isArchived: Boolean(v.isArchived),
})

export function PayeesPage() {
  const categories = useAsync(() => getTransactionCategories(), [])

  if (categories.loading && !categories.data) return <StateMessage title="Loading…" loading />
  if (categories.error) return <StateMessage title="Could not load categories" description={categories.error.message} />

  const fields: FieldSchema[] = [
    { name: 'name', label: 'Name', type: 'text', required: true },
    {
      name: 'defaultCategoryId',
      label: 'Default category',
      type: 'select',
      help: 'Pre-filled when this payee is picked on a transaction.',
      options: [
        { value: '', label: '— none —' },
        ...(categories.data ?? []).filter((c) => !c.isArchived).map((c) => ({ value: c.id, label: c.name })),
      ],
    },
    {
      name: 'defaultGstTreatment',
      label: 'Default GST treatment',
      type: 'select',
      options: [
        { value: '', label: '— none —' },
        { value: 'Taxable', label: 'Taxable' },
        { value: 'Exempt', label: 'Exempt' },
        { value: 'ZeroRated', label: 'Zero-rated' },
      ],
    },
    { name: 'notes', label: 'Notes', type: 'textarea' },
    { name: 'isArchived', label: 'Archived (hidden from pickers)', type: 'checkbox', defaultValue: false },
  ]

  return (
    <CrudSection<Payee>
      resourceName="Payee"
      title="Payees"
      description="The suppliers and payers the business deals with. Picking a payee on a transaction pre-fills its defaults; payees with history are archived rather than deleted so reports stay intact."
      load={() => getPayees(true)}
      getId={(p) => p.id}
      rowLabel={(p) => p.name}
      columns={[
        {
          header: 'Name',
          cell: (p) => (
            <>
              {p.name}
              {p.isArchived && (
                <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs text-slate-500">archived</span>
              )}
            </>
          ),
          className: 'font-medium text-slate-900',
        },
        { header: 'Default category', cell: (p) => orDash(p.defaultCategoryName) },
        {
          header: 'Default GST',
          cell: (p) => orDash(p.defaultGstTreatment === 'ZeroRated' ? 'Zero-rated' : p.defaultGstTreatment),
        },
        { header: 'Notes', cell: (p) => orDash(p.notes) },
      ]}
      fields={fields}
      onCreate={(v) => createPayee(toRequest(v)).then(() => undefined)}
      onUpdate={(id, v) => updatePayee(id, toRequest(v)).then(() => undefined)}
      onDelete={deletePayee}
      emptyText="No payees yet — they also appear here automatically once you start linking transactions."
    />
  )
}
