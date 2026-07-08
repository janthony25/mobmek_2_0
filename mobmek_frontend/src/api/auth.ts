import { apiGet, apiPost } from './client'
import type { CurrentUser, LoginRequest } from '@/types'

export const login = (body: LoginRequest) => apiPost<CurrentUser>('/auth/login', body)
export const logout = () => apiPost<void>('/auth/logout', undefined)
export const getCurrentUser = () => apiGet<CurrentUser>('/auth/me')
