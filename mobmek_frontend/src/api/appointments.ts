import { apiDelete, apiGet, apiPost, apiPut, apiUrl } from './client'
import type {
  Appointment,
  AppointmentStatus,
  CreateAppointmentRequest,
  CreateCarRequest,
  CustomerRequest,
  OutboundEmail,
  SendAppointmentEmailRequest,
  UpdateAppointmentRequest,
} from '@/types'

interface AppointmentQuery {
  from?: string
  to?: string
  status?: AppointmentStatus
  mechanicId?: string
  jobId?: string
  /** Matches title, contact name/phone, vehicle description, customer name and car rego. */
  search?: string
}

/** Appointments overlapping [from, to), ordered by start time. */
export const getAppointments = ({ from, to, status, mechanicId, jobId, search }: AppointmentQuery = {}) => {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (status !== undefined) params.set('status', String(status))
  if (mechanicId) params.set('mechanicId', mechanicId)
  if (jobId) params.set('jobId', jobId)
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
  // Must be carried through: this helper rebuilds the *whole* payload for a PUT, so omitting
  // a field here would blank it on the server every time a status is changed.
  contactEmail: a.contactEmail,
  vehicleDescription: a.vehicleDescription,
  customerId: a.customerId,
  carId: a.carId,
  jobId: a.jobId,
  mechanicId: a.mechanicId,
  ...patch,
})

/**
 * Server-Sent Events endpoint the calendar page subscribes to for live updates — one
 * "changed" event per appointment create/update/delete, including bookings from the public
 * website. Returns a plain URL (not a fetch helper) because EventSource takes a URL directly.
 */
export const appointmentsStreamUrl = () => apiUrl('/appointments/stream')

export const getAppointment = (id: string) => apiGet<Appointment>(`/appointments/${id}`)
export const createAppointment = (body: CreateAppointmentRequest) =>
  apiPost<Appointment>('/appointments', body)
export const updateAppointment = (id: string, body: UpdateAppointmentRequest) =>
  apiPut<Appointment>(`/appointments/${id}`, body)
export const deleteAppointment = (id: string) => apiDelete(`/appointments/${id}`)

/** Convert-on-arrival step 1: creates a customer from the phone-call contact and atomically
 * links it to the appointment in one request — see docs/features/appointments-calendar-sync.md. */
export const convertAppointmentToCustomer = (id: string, body: CustomerRequest) =>
  apiPost<Appointment>(`/appointments/${id}/convert-to-customer`, body)

/** Convert-on-arrival step 2: creates a car for the appointment's linked customer and
 * atomically links it to the appointment in one request. */
export const convertAppointmentToCar = (id: string, body: CreateCarRequest) =>
  apiPost<Appointment>(`/appointments/${id}/convert-to-car`, body)

export const sendAppointmentEmail = (id: string, body: SendAppointmentEmailRequest) =>
  apiPost<OutboundEmail>(`/appointments/${id}/email`, body)
