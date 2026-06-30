import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { EmploymentType, NamedLookupRequest } from '@/types'

export const getEmploymentTypes = () => apiGet<EmploymentType[]>('/employmenttypes')
export const createEmploymentType = (body: NamedLookupRequest) =>
  apiPost<EmploymentType>('/employmenttypes', body)
export const updateEmploymentType = (id: string, body: NamedLookupRequest) =>
  apiPut<EmploymentType>(`/employmenttypes/${id}`, body)
export const deleteEmploymentType = (id: string) => apiDelete(`/employmenttypes/${id}`)
