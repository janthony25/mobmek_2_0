import {
  createEmploymentType,
  deleteEmploymentType,
  getEmploymentTypes,
  updateEmploymentType,
} from '@/api/employmentTypes'
import { LookupCrudPage } from './LookupCrudPage'

export function EmploymentTypesPage() {
  return (
    <LookupCrudPage
      resourceName="Employment Type"
      title="Employment Types"
      description="Ways an employee can be engaged (e.g. full-time, contractor)."
      load={getEmploymentTypes}
      create={createEmploymentType}
      update={updateEmploymentType}
      remove={deleteEmploymentType}
    />
  )
}
