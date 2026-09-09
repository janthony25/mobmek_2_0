import { apiDelete, apiGet, apiPostForm, apiUrl } from './client'
import type { JobPhoto } from '@/types'

const base = (jobId: string) => `/jobs/${jobId}/photos`

export const getJobPhotos = (jobId: string) => apiGet<JobPhoto[]>(base(jobId))

export const addJobPhoto = (jobId: string, file: File) => {
  const form = new FormData()
  form.append('file', file)
  return apiPostForm<JobPhoto>(base(jobId), form)
}

export const deleteJobPhoto = (jobId: string, photoId: string) =>
  apiDelete(`${base(jobId)}/${photoId}`)

/** Image URL for a job photo; hand it to an <img src> or an <a href>. */
export const jobPhotoUrl = (jobId: string, photoId: string) =>
  apiUrl(`${base(jobId)}/${photoId}`)
