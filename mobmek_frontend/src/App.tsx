import { Navigate, Route, Routes } from 'react-router-dom'
import { useNumberInputWheelGuard } from '@/hooks/useNumberInputWheelGuard'
import { AppLayout } from '@/components/layout/AppLayout'
import { RequireAuth } from '@/components/auth/RequireAuth'
import { RequireAdmin } from '@/components/auth/RequireAdmin'
import { LoginPage } from '@/pages/LoginPage'
import { CustomersPage } from '@/pages/CustomersPage'
import { CustomerDetailPage } from '@/pages/CustomerDetailPage'
import { CarDetailPage } from '@/pages/CarDetailPage'
import { JobCenterPage } from '@/pages/JobCenterPage'
import { AppointmentsPage } from '@/pages/AppointmentsPage'
import { NotesRemindersPage } from '@/pages/NotesRemindersPage'
import { NewJobPage } from '@/pages/NewJobPage'
import { JobDetailPage } from '@/pages/JobDetailPage'
import { InvoicesPage } from '@/pages/InvoicesPage'
import { QuotationsPage } from '@/pages/QuotationsPage'
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
import { CashFlowPage } from '@/pages/CashFlowPage'
import { CashAccountsPage } from '@/pages/CashAccountsPage'
import { TransactionCategoriesPage } from '@/pages/TransactionCategoriesPage'
import { RecurringPlannedPage } from '@/pages/RecurringPlannedPage'
import { ForecastPage } from '@/pages/ForecastPage'
import { GstReportPage } from '@/pages/GstReportPage'
import { PayeesPage } from '@/pages/PayeesPage'
import { CategorizationRulesPage } from '@/pages/CategorizationRulesPage'

function App() {
  useNumberInputWheelGuard()
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        {/* Outside AppLayout: a bare, print-friendly page with no sidebar/notes panel. */}
        <Route path="jobs/:jobId/invoices/:invoiceId/pdf" element={<InvoicePrintPage />} />

        <Route element={<AppLayout />}>
          <Route index element={<Navigate to="/customers" replace />} />
          <Route path="customers" element={<CustomersPage />} />
          <Route path="customers/:id" element={<CustomerDetailPage />} />
          <Route path="customers/:customerId/cars/:carId" element={<CarDetailPage />} />
          <Route path="appointments" element={<AppointmentsPage />} />
          <Route path="notes-reminders" element={<NotesRemindersPage />} />
          <Route path="jobs" element={<JobCenterPage />} />
          <Route path="jobs/new" element={<NewJobPage />} />
          <Route path="jobs/:id" element={<JobDetailPage />} />
          <Route path="invoices" element={<InvoicesPage />} />
          <Route path="quotations" element={<QuotationsPage />} />
          <Route path="car-makes" element={<CarMakesPage />} />
          <Route path="products" element={<ProductsPage />} />
          <Route path="services" element={<JobServicesPage />} />
          <Route path="reminder-templates" element={<ReminderTemplatesPage />} />

          {/* Admin-only: HR, settings, and financials — mirrors [Authorize(Roles = "Admin")] on the API. */}
          <Route element={<RequireAdmin />}>
            <Route path="employees" element={<EmployeesPage />} />
            <Route path="employee-titles" element={<EmployeeTitlesPage />} />
            <Route path="employment-types" element={<EmploymentTypesPage />} />
            <Route path="cash-flow" element={<CashFlowPage />} />
            <Route path="cash-accounts" element={<CashAccountsPage />} />
            <Route path="transaction-categories" element={<TransactionCategoriesPage />} />
            <Route path="payees" element={<PayeesPage />} />
            <Route path="categorization-rules" element={<CategorizationRulesPage />} />
            <Route path="recurring-planned" element={<RecurringPlannedPage />} />
            <Route path="forecast" element={<ForecastPage />} />
            <Route path="gst-report" element={<GstReportPage />} />
            <Route path="tax" element={<TaxSettingsPage />} />
            <Route path="business-details" element={<BusinessDetailsSettingsPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/customers" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}

export default App
