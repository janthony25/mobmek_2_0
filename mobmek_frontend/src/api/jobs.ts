import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CreateJobRequest, Job, UpdateJobRequest } from '@/types'

export const getJobs = (customerId?: string) =>
  apiGet<Job[]>(`/jobs${customerId ? `?customerId=${encodeURIComponent(customerId)}` : ''}`)
export const getJob = (id: string) => apiGet<Job>(`/jobs/${id}`)
export const createJob = (body: CreateJobRequest) => apiPost<Job>('/jobs', body)
export const updateJob = (id: string, body: UpdateJobRequest) => apiPut<Job>(`/jobs/${id}`, body)
export const deleteJob = (id: string) => apiDelete(`/jobs/${id}`)

export const addJobMechanic = (jobId: string, employeeId: string) =>
  apiPost<Job>(`/jobs/${jobId}/mechanics`, { employeeId })
export const removeJobMechanic = (jobId: string, employeeId: string) =>
  apiDelete(`/jobs/${jobId}/mechanics/${employeeId}`)
