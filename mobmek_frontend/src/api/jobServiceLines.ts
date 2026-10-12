import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CreateJobServiceLineRequest, JobServiceLine, UpdateJobServiceLineRequest } from '@/types'

const base = (jobId: string) => `/jobs/${encodeURIComponent(jobId)}/services`

export const getJobServiceLines = (jobId: string) => apiGet<JobServiceLine[]>(base(jobId))
export const createJobServiceLine = (jobId: string, body: CreateJobServiceLineRequest) =>
  apiPost<JobServiceLine>(base(jobId), body)
export const updateJobServiceLine = (jobId: string, id: string, body: UpdateJobServiceLineRequest) =>
  apiPut<JobServiceLine>(`${base(jobId)}/${id}`, body)
export const deleteJobServiceLine = (jobId: string, id: string) =>
  apiDelete(`${base(jobId)}/${id}`)
