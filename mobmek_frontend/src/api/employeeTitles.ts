import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { EmployeeTitle, NamedLookupRequest } from '@/types'

export const getEmployeeTitles = () => apiGet<EmployeeTitle[]>('/employeetitles')
export const createEmployeeTitle = (body: NamedLookupRequest) =>
  apiPost<EmployeeTitle>('/employeetitles', body)
export const updateEmployeeTitle = (id: string, body: NamedLookupRequest) =>
  apiPut<EmployeeTitle>(`/employeetitles/${id}`, body)
export const deleteEmployeeTitle = (id: string) => apiDelete(`/employeetitles/${id}`)
