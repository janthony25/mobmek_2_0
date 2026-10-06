import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { getCurrentUser, login as apiLogin, logout as apiLogout } from '@/api/auth'
import { setUnauthorizedHandler } from '@/api/client'
import type { CurrentUser } from '@/types'

interface AuthContextValue {
  user: CurrentUser | null
  /** True only while the initial session check (GET /auth/me) is in flight. */
  loading: boolean
  /** Still literally "do they hold the Admin role" — fine for the few things that really mean
   * that specifically (e.g. the "· Admin" badge). For gating a feature, use hasPermission
   * instead: a role's permissions are admin-editable, so "Admin" alone isn't the right check. */
  isAdmin: boolean
  hasPermission: (permission: string) => boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getCurrentUser()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null))
    return () => setUnauthorizedHandler(null)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    setUser(await apiLogin({ email, password }))
  }, [])

  const logout = useCallback(async () => {
    await apiLogout()
    setUser(null)
  }, [])

  const hasPermission = useCallback(
    (permission: string) => user?.permissions.includes(permission) ?? false,
    [user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({ user, loading, isAdmin: user?.roles.includes('Admin') ?? false, hasPermission, login, logout }),
    [user, loading, hasPermission, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
