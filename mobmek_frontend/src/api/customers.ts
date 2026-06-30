import { apiGet } from './client'
import type { Customer } from '@/types'

export function getCustomers(): Promise<Customer[]> {
  return apiGet<Customer[]>('/customers')
}
