import {
  createTransactionCategory,
  deleteTransactionCategory,
  getTransactionCategories,
  updateTransactionCategory,
} from '@/api/transactionCategories'
import { CrudSection } from '@/components/crud/CrudSection'
import type { FieldSchema } from '@/components/crud/types'
import type { TransactionCategory, TransactionCategoryRequest } from '@/types'

const fields: FieldSchema[] = [
  { name: 'name', label: 'Name', type: 'text', required: true },
  {
    name: 'direction',
    label: 'Applies to',
    type: 'select',
    required: true,
    defaultValue: 'Out',
    options: [
      { value: 'In', label: 'Money in' },
      { value: 'Out', label: 'Money out' },
      { value: 'Either', label: 'Either' },
    ],
  },
  { name: 'group', label: 'Group', type: 'text', required: true, defaultValue: 'Operating', help: 'Report rollup, e.g. Operating, Payroll, Taxes, Financing.' },
  {
    name: 'defaultGstTreatment',
    label: 'Default GST treatment',
    type: 'select',
    defaultValue: 'Taxable',
    options: [
      { value: 'Taxable', label: 'Taxable' },
      { value: 'Exempt', label: 'Exempt' },
      { value: 'ZeroRated', label: 'Zero-rated' },
    ],
  },
  {
    name: 'excludeFromOperatingExpense',
    label: 'Exclude from operating figures (tax remittances, loans, drawings)',
    type: 'checkbox',
    defaultValue: false,
  },
  { name: 'isArchived', label: 'Archived (hidden from pickers)', type: 'checkbox', defaultValue: false },
]

export function TransactionCategoriesPage() {
  return (
    <CrudSection<TransactionCategory>
      resourceName="Category"
      title="Transaction Categories"
      description="How cash movements are classified. Built-in categories (marked “system”) can be renamed or archived but not deleted, and keep their direction and GST defaults."
      load={() => getTransactionCategories(true)}
      getId={(c) => c.id}
      rowLabel={(c) => c.name}
      columns={[
        {
          header: 'Name',
          cell: (c) => (
            <>
              {c.name}
              {c.isSystem && (
                <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs text-slate-500">system</span>
              )}
              {c.isArchived && (
                <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs text-slate-500">archived</span>
              )}
            </>
          ),
          className: 'font-medium text-slate-900',
        },
        { header: 'Group', cell: (c) => c.group },
        {
          header: 'Applies to',
          cell: (c) => (c.direction === 'In' ? 'Money in' : c.direction === 'Out' ? 'Money out' : 'Either'),
        },
        { header: 'GST default', cell: (c) => (c.defaultGstTreatment === 'ZeroRated' ? 'Zero-rated' : c.defaultGstTreatment) },
        { header: 'Operating figures', cell: (c) => (c.excludeFromOperatingExpense ? 'Excluded' : 'Included') },
      ]}
      fields={fields}
      onCreate={(v) => createTransactionCategory(v as unknown as TransactionCategoryRequest).then(() => undefined)}
      onUpdate={(id, v) => updateTransactionCategory(id, v as unknown as TransactionCategoryRequest).then(() => undefined)}
      onDelete={deleteTransactionCategory}
    />
  )
}
