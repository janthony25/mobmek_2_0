import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Labour, LabourRequest } from '@/types'

const base = (jobId: string) => `/jobs/${encodeURIComponent(jobId)}/labour`

export const getLabour = (jobId: string) => apiGet<Labour[]>(base(jobId))
export const createLabour = (jobId: string, body: LabourRequest) =>
  apiPost<Labour>(base(jobId), body)
export const updateLabour = (jobId: string, id: string, body: LabourRequest) =>
  apiPut<Labour>(`${base(jobId)}/${id}`, body)
export const deleteLabour = (jobId: string, id: string) => apiDelete(`${base(jobId)}/${id}`)
