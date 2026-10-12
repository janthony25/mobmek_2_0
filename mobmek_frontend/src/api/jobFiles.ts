import { apiDelete, apiGet, apiPostForm, apiUrl } from './client'
import type { JobFile } from '@/types'

const base = (jobId: string) => `/jobs/${jobId}/files`

export const getJobFiles = (jobId: string) => apiGet<JobFile[]>(base(jobId))

export const addJobFile = (jobId: string, file: File) => {
  const form = new FormData()
  form.append('file', file)
  return apiPostForm<JobFile>(base(jobId), form)
}

export const deleteJobFile = (jobId: string, fileId: string) =>
  apiDelete(`${base(jobId)}/${fileId}`)

/** Download URL for a job file; hand it to an <a href>. */
export const jobFileUrl = (jobId: string, fileId: string) =>
  apiUrl(`${base(jobId)}/${fileId}`)
