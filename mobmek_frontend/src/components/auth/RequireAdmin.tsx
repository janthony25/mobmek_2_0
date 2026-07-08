import { useEffect } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { useToast } from '@/components/ui/toast'

/** Nested inside RequireAuth — assumes a session already exists, only checks the role. */
export function RequireAdmin() {
  const { isAdmin } = useAuth()
  const toast = useToast()

  useEffect(() => {
    if (!isAdmin) toast.error("You don't have permission to view that page.")
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin])

  return isAdmin ? <Outlet /> : <Navigate to="/customers" replace />
}
