import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type {
  Appointment,
  AppointmentStatus,
  CreateAppointmentRequest,
  UpdateAppointmentRequest,
} from '@/types'

interface AppointmentQuery {
  from?: string
  to?: string
  status?: AppointmentStatus
  mechanicId?: string
  /** Matches title, contact name/phone, vehicle description, customer name and car rego. */
  search?: string
}

/** Appointments overlapping [from, to), ordered by start time. */
export const getAppointments = ({ from, to, status, mechanicId, search }: AppointmentQuery = {}) => {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (status !== undefined) params.set('status', String(status))
  if (mechanicId) params.set('mechanicId', mechanicId)
  if (search) params.set('search', search)
  const qs = params.toString()
  return apiGet<Appointment[]>(`/appointments${qs ? `?${qs}` : ''}`)
}

/** An appointment's current fields as an update payload, with optional overrides. */
export const toAppointmentRequest = (
  a: Appointment,
  patch: Partial<CreateAppointmentRequest> = {},
): UpdateAppointmentRequest => ({
  title: a.title,
  startUtc: a.startUtc,
  endUtc: a.endUtc,
  status: a.status,
  notes: a.notes,
  contactName: a.contactName,
  contactPhone: a.contactPhone,
  vehicleDescription: a.vehicleDescription,
  customerId: a.customerId,
  carId: a.carId,
  jobId: a.jobId,
  mechanicId: a.mechanicId,
  ...patch,
})

export const getAppointment = (id: string) => apiGet<Appointment>(`/appointments/${id}`)
export const createAppointment = (body: CreateAppointmentRequest) =>
  apiPost<Appointment>('/appointments', body)
export const updateAppointment = (id: string, body: UpdateAppointmentRequest) =>
  apiPut<Appointment>(`/appointments/${id}`, body)
export const deleteAppointment = (id: string) => apiDelete(`/appointments/${id}`)
