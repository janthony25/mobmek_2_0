import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { ReminderTemplate, ReminderTemplateRequest } from '@/types'

export const getReminderTemplates = () => apiGet<ReminderTemplate[]>('/remindertemplates')
export const createReminderTemplate = (body: ReminderTemplateRequest) =>
  apiPost<ReminderTemplate>('/remindertemplates', body)
export const updateReminderTemplate = (id: string, body: ReminderTemplateRequest) =>
  apiPut<ReminderTemplate>(`/remindertemplates/${id}`, body)
export const deleteReminderTemplate = (id: string) => apiDelete(`/remindertemplates/${id}`)
