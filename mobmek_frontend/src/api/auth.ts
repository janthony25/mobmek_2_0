import { apiGet, apiPost } from './client'
import type { CurrentUser, ForgotPasswordRequest, LoginRequest, ResetForgottenPasswordRequest } from '@/types'

export const login = (body: LoginRequest) => apiPost<CurrentUser>('/auth/login', body)
export const logout = () => apiPost<void>('/auth/logout', undefined)
export const getCurrentUser = () => apiGet<CurrentUser>('/auth/me')
export const forgotPassword = (body: ForgotPasswordRequest) => apiPost<void>('/auth/forgot-password', body)
export const resetForgottenPassword = (body: ResetForgottenPasswordRequest) => apiPost<void>('/auth/reset-password', body)
