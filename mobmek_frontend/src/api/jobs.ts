import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CreateJobRequest, Job, JobStatus, PagedResult, UpdateJobRequest } from '@/types'

export interface JobPagedFilters {
  sortBy?: 'newest' | 'oldest'
  status?: JobStatus
  dateFrom?: string
  dateTo?: string
}

export const getJobs = (customerId?: string) =>
  apiGet<Job[]>(`/jobs${customerId ? `?customerId=${encodeURIComponent(customerId)}` : ''}`)
export const getJobsPaged = (page: number, pageSize: number, search?: string, filters?: JobPagedFilters) => {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (search) params.set('search', search)
  if (filters?.sortBy) params.set('sortBy', filters.sortBy)
  if (filters?.status !== undefined) params.set('status', String(filters.status))
  if (filters?.dateFrom) params.set('dateFrom', filters.dateFrom)
  if (filters?.dateTo) params.set('dateTo', filters.dateTo)
  return apiGet<PagedResult<Job>>(`/jobs/paged?${params}`)
}
export const getJob = (id: string) => apiGet<Job>(`/jobs/${id}`)
export const createJob = (body: CreateJobRequest) => apiPost<Job>('/jobs', body)
export const updateJob = (id: string, body: UpdateJobRequest) => apiPut<Job>(`/jobs/${id}`, body)
export const deleteJob = (id: string) => apiDelete(`/jobs/${id}`)

export const addJobMechanic = (jobId: string, employeeId: string) =>
  apiPost<Job>(`/jobs/${jobId}/mechanics`, { employeeId })
export const removeJobMechanic = (jobId: string, employeeId: string) =>
  apiDelete(`/jobs/${jobId}/mechanics/${employeeId}`)
