import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Customer, CustomerRequest } from '@/types'

export const getCustomers = () => apiGet<Customer[]>('/customers')
export const getCustomer = (id: string) => apiGet<Customer>(`/customers/${id}`)
export const createCustomer = (body: CustomerRequest) => apiPost<Customer>('/customers', body)
export const updateCustomer = (id: string, body: CustomerRequest) =>
  apiPut<Customer>(`/customers/${id}`, body)
export const deleteCustomer = (id: string) => apiDelete(`/customers/${id}`)
