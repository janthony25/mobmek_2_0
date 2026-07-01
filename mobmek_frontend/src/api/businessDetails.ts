import { apiGet, apiPut } from './client'
import type { BusinessDetails, UpdateBusinessDetailsRequest } from '@/types'

export const getBusinessDetails = () => apiGet<BusinessDetails>('/business-details')
export const updateBusinessDetails = (body: UpdateBusinessDetailsRequest) =>
  apiPut<BusinessDetails>('/business-details', body)
