import { Navigate, Route, Routes } from 'react-router-dom'
import { useNumberInputWheelGuard } from '@/hooks/useNumberInputWheelGuard'
import { AppLayout } from '@/components/layout/AppLayout'
import { CustomersPage } from '@/pages/CustomersPage'
import { CustomerDetailPage } from '@/pages/CustomerDetailPage'
import { CarDetailPage } from '@/pages/CarDetailPage'
import { JobCenterPage } from '@/pages/JobCenterPage'
import { NewJobPage } from '@/pages/NewJobPage'
import { JobDetailPage } from '@/pages/JobDetailPage'
import { CarMakesPage } from '@/pages/CarMakesPage'
import { ProductsPage } from '@/pages/ProductsPage'
import { JobServicesPage } from '@/pages/JobServicesPage'
import { EmployeesPage } from '@/pages/EmployeesPage'
import { EmployeeTitlesPage } from '@/pages/EmployeeTitlesPage'
import { EmploymentTypesPage } from '@/pages/EmploymentTypesPage'
import { TaxSettingsPage } from '@/pages/TaxSettingsPage'
import { BusinessDetailsSettingsPage } from '@/pages/BusinessDetailsSettingsPage'
import { ReminderTemplatesPage } from '@/pages/ReminderTemplatesPage'
import { InvoicePrintPage } from '@/pages/InvoicePrintPage'

function App() {
  useNumberInputWheelGuard()
  return (
    <Routes>
      {/* Outside AppLayout: a bare, print-friendly page with no sidebar/notes panel. */}
      <Route path="jobs/:jobId/invoices/:invoiceId/pdf" element={<InvoicePrintPage />} />

      <Route element={<AppLayout />}>
        <Route index element={<Navigate to="/customers" replace />} />
        <Route path="customers" element={<CustomersPage />} />
        <Route path="customers/:id" element={<CustomerDetailPage />} />
        <Route path="customers/:customerId/cars/:carId" element={<CarDetailPage />} />
        <Route path="jobs" element={<JobCenterPage />} />
        <Route path="jobs/new" element={<NewJobPage />} />
        <Route path="jobs/:id" element={<JobDetailPage />} />
        <Route path="car-makes" element={<CarMakesPage />} />
        <Route path="products" element={<ProductsPage />} />
        <Route path="services" element={<JobServicesPage />} />
        <Route path="employees" element={<EmployeesPage />} />
        <Route path="employee-titles" element={<EmployeeTitlesPage />} />
        <Route path="employment-types" element={<EmploymentTypesPage />} />
        <Route path="tax" element={<TaxSettingsPage />} />
        <Route path="business-details" element={<BusinessDetailsSettingsPage />} />
        <Route path="reminder-templates" element={<ReminderTemplatesPage />} />
        <Route path="*" element={<Navigate to="/customers" replace />} />
      </Route>
    </Routes>
  )
}

export default App
