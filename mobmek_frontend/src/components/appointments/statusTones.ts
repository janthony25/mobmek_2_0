import type { Tone } from '@/components/ui/Badge'
import { AppointmentStatus } from '@/types'

/** Badge tone per appointment status, shared by the calendar page and detail modal. */
export const APPOINTMENT_STATUS_TONES: Record<AppointmentStatus, Tone> = {
  [AppointmentStatus.Scheduled]: 'blue',
  [AppointmentStatus.Confirmed]: 'green',
  [AppointmentStatus.Arrived]: 'amber',
  [AppointmentStatus.Completed]: 'slate',
  [AppointmentStatus.NoShow]: 'red',
  [AppointmentStatus.Cancelled]: 'slate',
  // Orange is used by nothing else, so website requests stand out as needing a decision.
  [AppointmentStatus.Requested]: 'orange',
}
