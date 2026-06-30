import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { CustomersPage } from '@/pages/CustomersPage'
import { JobCenterPage } from '@/pages/JobCenterPage'
import { CarMakesPage } from '@/pages/CarMakesPage'

function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<Navigate to="/customers" replace />} />
        <Route path="customers" element={<CustomersPage />} />
        <Route path="jobs" element={<JobCenterPage />} />
        <Route path="car-makes" element={<CarMakesPage />} />
        <Route path="*" element={<Navigate to="/customers" replace />} />
      </Route>
    </Routes>
  )
}

export default App
