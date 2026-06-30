import { apiGet, apiPut } from './client'
import type { GstSetting } from '@/types'

export const getGstSetting = () => apiGet<GstSetting>('/gst')
export const updateGstSetting = (rate: number) => apiPut<GstSetting>('/gst', { rate })
