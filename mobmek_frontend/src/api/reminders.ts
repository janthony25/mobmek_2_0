import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { CreateReminderRequest, Reminder, UpdateReminderRequest } from '@/types'

interface ReminderQuery {
  customerId?: string
  carId?: string
  includeDone?: boolean
}

export const getReminders = (query: ReminderQuery = {}) => {
  const params = new URLSearchParams()
  if (query.customerId) params.set('customerId', query.customerId)
  if (query.carId) params.set('carId', query.carId)
  if (query.includeDone !== undefined) params.set('includeDone', String(query.includeDone))
  const qs = params.toString()
  return apiGet<Reminder[]>(`/reminders${qs ? `?${qs}` : ''}`)
}

export const createReminder = (body: CreateReminderRequest) => apiPost<Reminder>('/reminders', body)
export const updateReminder = (id: string, body: UpdateReminderRequest) =>
  apiPut<Reminder>(`/reminders/${id}`, body)
export const deleteReminder = (id: string) => apiDelete(`/reminders/${id}`)
