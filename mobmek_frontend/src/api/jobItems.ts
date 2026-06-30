import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { JobItem, JobItemRequest } from '@/types'

const base = (jobId: string) => `/jobs/${encodeURIComponent(jobId)}/items`

export const getJobItems = (jobId: string) => apiGet<JobItem[]>(base(jobId))
export const createJobItem = (jobId: string, body: JobItemRequest) =>
  apiPost<JobItem>(base(jobId), body)
export const updateJobItem = (jobId: string, id: string, body: JobItemRequest) =>
  apiPut<JobItem>(`${base(jobId)}/${id}`, body)
export const deleteJobItem = (jobId: string, id: string) => apiDelete(`${base(jobId)}/${id}`)
