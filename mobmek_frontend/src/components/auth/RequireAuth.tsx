import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { StateMessage } from '@/components/ui/StateMessage'

/** Gate on the whole app shell: redirects to /login when there's no signed-in user. */
export function RequireAuth() {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center bg-slate-50">
        <StateMessage loading title="Checking session…" />
      </div>
    )
  }

  if (!user) {
    // Don't remember the kiosk-only page as somewhere to return to — the next sign-in on this
    // browser/tablet could be a completely different account (e.g. an Admin doing maintenance),
    // and sending them straight back into the nav-free kiosk page looked like a bug (they'd pass
    // RequirePermission trivially, since Admin holds every permission).
    const from = location.pathname === '/add-customer-car' ? undefined : location.pathname
    return <Navigate to="/login" replace state={{ from }} />
  }

  return <Outlet />
}
