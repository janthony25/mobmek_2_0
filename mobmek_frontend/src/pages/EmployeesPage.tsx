import { createEmployee, deleteEmployee, getEmployees, updateEmployee } from '@/api/employees'
import { getEmployeeTitles } from '@/api/employeeTitles'
import { getEmploymentTypes } from '@/api/employmentTypes'
import { CrudSection } from '@/components/crud/CrudSection'
import { StateMessage } from '@/components/ui/StateMessage'
import type { FieldSchema } from '@/components/crud/types'
import { useAsync } from '@/hooks/useAsync'
import { orDash } from '@/lib/format'
import type { Employee, EmployeeRequest } from '@/types'

export function EmployeesPage() {
  // Title / employment-type selects need their options loaded up front.
  const titles = useAsync(getEmployeeTitles, [])
  const types = useAsync(getEmploymentTypes, [])

  if (titles.loading || types.loading) return <StateMessage title="Loading…" />
  if (titles.error || types.error) {
    return <StateMessage title="Could not load reference data" description={(titles.error ?? types.error)?.message} />
  }

  const fields: FieldSchema[] = [
    { name: 'firstName', label: 'First name', type: 'text', required: true },
    { name: 'lastName', label: 'Last name', type: 'text', required: true },
    {
      name: 'titleId',
      label: 'Title',
      type: 'select',
      required: true,
      options: (titles.data ?? []).map((t) => ({ value: t.id, label: t.name })),
    },
    {
      name: 'employmentTypeId',
      label: 'Employment type',
      type: 'select',
      required: true,
      options: (types.data ?? []).map((t) => ({ value: t.id, label: t.name })),
    },
    { name: 'contactNumber', label: 'Contact number', type: 'text', required: true },
    { name: 'emailAddress', label: 'Email', type: 'text', required: true },
    { name: 'physicalAddress', label: 'Address', type: 'text', required: true },
  ]

  return (
    <CrudSection<Employee>
      resourceName="Employee"
      description="Workshop staff and mechanics."
      load={getEmployees}
      getId={(e) => e.id}
      rowLabel={(e) => `${e.firstName} ${e.lastName}`}
      columns={[
        {
          header: 'Name',
          cell: (e) => `${e.firstName} ${e.lastName}`,
          className: 'font-medium text-slate-900',
        },
        { header: 'Title', cell: (e) => orDash(e.titleName) },
        { header: 'Type', cell: (e) => orDash(e.employmentTypeName) },
        { header: 'Contact', cell: (e) => e.contactNumber },
        { header: 'Email', cell: (e) => e.emailAddress },
      ]}
      fields={fields}
      onCreate={(v) => createEmployee(v as unknown as EmployeeRequest).then(() => undefined)}
      onUpdate={(id, v) => updateEmployee(id, v as unknown as EmployeeRequest).then(() => undefined)}
      onDelete={deleteEmployee}
    />
  )
}
