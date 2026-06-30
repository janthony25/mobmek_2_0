import { apiGet } from './client'
import type { Job } from '@/types'

export function getJobs(customerId?: string): Promise<Job[]> {
  const query = customerId ? `?customerId=${encodeURIComponent(customerId)}` : ''
  return apiGet<Job[]>(`/jobs${query}`)
}
