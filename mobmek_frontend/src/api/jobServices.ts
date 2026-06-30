import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { JobService, JobServiceRequest } from '@/types'

export const getJobServices = (activeOnly?: boolean) =>
  apiGet<JobService[]>(`/jobservices${activeOnly ? '?activeOnly=true' : ''}`)
export const createJobService = (body: JobServiceRequest) =>
  apiPost<JobService>('/jobservices', body)
export const updateJobService = (id: string, body: JobServiceRequest) =>
  apiPut<JobService>(`/jobservices/${id}`, body)
export const deleteJobService = (id: string) => apiDelete(`/jobservices/${id}`)
