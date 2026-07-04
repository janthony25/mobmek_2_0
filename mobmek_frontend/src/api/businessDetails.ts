import { apiDelete, apiGet, apiPostForm, apiPut, apiUrl } from './client'
import type { BusinessDetails, UpdateBusinessDetailsRequest } from '@/types'

export const getBusinessDetails = () => apiGet<BusinessDetails>('/business-details')
export const updateBusinessDetails = (body: UpdateBusinessDetailsRequest) =>
  apiPut<BusinessDetails>('/business-details', body)

export const uploadBusinessLogo = (file: File) => {
  const form = new FormData()
  form.append('file', file)
  return apiPostForm<BusinessDetails>('/business-details/logo', form)
}

export const deleteBusinessLogo = () => apiDelete('/business-details/logo')

/** Absolute-ish URL for the <img> tag; hand it `data.logoUrl` (already the `/business-details/logo` path). */
export const businessLogoUrl = (logoUrl: string) => apiUrl(logoUrl)
