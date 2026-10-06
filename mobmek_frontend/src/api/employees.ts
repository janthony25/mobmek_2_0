import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Employee, EmployeeRequest, EmployeeSummary } from '@/types'

export const getEmployees = () => apiGet<Employee[]>('/employees')
/** Name-only list for pickers — open to any signed-in staff member, unlike getEmployees(). */
export const getEmployeeSummaries = () => apiGet<EmployeeSummary[]>('/employees/summary')
export const createEmployee = (body: EmployeeRequest) => apiPost<Employee>('/employees', body)
export const updateEmployee = (id: string, body: EmployeeRequest) =>
  apiPut<Employee>(`/employees/${id}`, body)
export const deleteEmployee = (id: string) => apiDelete(`/employees/${id}`)
