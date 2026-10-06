import { useEffect } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { useToast } from '@/components/ui/toast'

/** Nested inside RequireAuth — assumes a session already exists, only checks the permission.
 * Mirrors the API's [Authorize(Policy = Permissions.X)] on the routes it wraps — keep them in
 * sync (see App.tsx's route groups). */
export function RequirePermission({ permission }: { permission: string }) {
  const { hasPermission } = useAuth()
  const allowed = hasPermission(permission)
  const toast = useToast()

  useEffect(() => {
    if (!allowed) toast.error("You don't have permission to view that page.")
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [allowed])

  return allowed ? <Outlet /> : <Navigate to="/customers" replace />
}
