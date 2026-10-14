import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { PERMISSIONS } from '@/types'

/** Nested inside RequireAuth, wrapping the normal AppLayout shell. A signed-in user whose entire
 * permission set is exactly { AddCustomerAndCar } — the dedicated self-service kiosk account — is
 * sent to the standalone /add-customer-car page instead, on every path, so they never see the
 * sidebar/nav or any other page. */
export function KioskRedirect() {
  const { user } = useAuth()
  const kioskOnly = user?.permissions.length === 1 && user.permissions[0] === PERMISSIONS.AddCustomerAndCar

  if (kioskOnly) return <Navigate to="/add-customer-car" replace />

  return <Outlet />
}
