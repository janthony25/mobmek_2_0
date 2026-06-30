import {
  createEmployeeTitle,
  deleteEmployeeTitle,
  getEmployeeTitles,
  updateEmployeeTitle,
} from '@/api/employeeTitles'
import { LookupCrudPage } from './LookupCrudPage'

export function EmployeeTitlesPage() {
  return (
    <LookupCrudPage
      resourceName="Title"
      title="Employee Titles"
      description="Job titles employees can hold."
      load={getEmployeeTitles}
      create={createEmployeeTitle}
      update={updateEmployeeTitle}
      remove={deleteEmployeeTitle}
    />
  )
}
