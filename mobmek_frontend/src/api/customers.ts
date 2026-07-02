import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Customer, CustomerListItem, CustomerRequest, PagedResult } from '@/types'

export const getCustomers = () => apiGet<Customer[]>('/customers')
export const getCustomersPaged = (page: number, pageSize: number, search?: string) => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  return apiGet<PagedResult<CustomerListItem>>(`/customers/paged?${params}`)
}
export const getCustomer = (id: string) => apiGet<Customer>(`/customers/${id}`)
export const createCustomer = (body: CustomerRequest) => apiPost<Customer>('/customers', body)
export const updateCustomer = (id: string, body: CustomerRequest) =>
  apiPut<Customer>(`/customers/${id}`, body)
export const deleteCustomer = (id: string) => apiDelete(`/customers/${id}`)
