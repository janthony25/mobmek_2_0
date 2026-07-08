import { apiGet, apiPost, apiPut } from './client'
import type { ConfirmPasswordChangeRequest, Profile, UpdateProfileRequest } from '@/types'

export const getProfile = () => apiGet<Profile>('/account/profile')
export const updateProfile = (body: UpdateProfileRequest) => apiPut<Profile>('/account/profile', body)
export const requestPasswordChangeCode = () => apiPost<void>('/account/password/request-code', {})
export const confirmPasswordChange = (body: ConfirmPasswordChangeRequest) =>
  apiPost<void>('/account/password/confirm', body)
