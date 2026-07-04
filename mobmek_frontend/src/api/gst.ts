import { apiGet, apiPut } from './client'
import type { GstReport, GstSetting } from '@/types'

export const getGstSetting = () => apiGet<GstSetting>('/gst')
export const updateGstSetting = (rate: number) => apiPut<GstSetting>('/gst', { rate })

export const getGstReport = (start: string, end: string) =>
  apiGet<GstReport>(`/gst/report?start=${start}&end=${end}`)
