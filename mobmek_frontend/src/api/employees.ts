import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Employee, EmployeeRequest } from '@/types'

export const getEmployees = () => apiGet<Employee[]>('/employees')
export const createEmployee = (body: EmployeeRequest) => apiPost<Employee>('/employees', body)
export const updateEmployee = (id: string, body: EmployeeRequest) =>
  apiPut<Employee>(`/employees/${id}`, body)
export const deleteEmployee = (id: string) => apiDelete(`/employees/${id}`)
