import type { Tone } from '@/components/ui/Badge'
import { JobStatus } from '@/types'

/** Colour tone per job status, for the appointment-history status badges. */
export const JOB_STATUS_TONE: Record<JobStatus, Tone> = {
  [JobStatus.Open]: 'slate',
  [JobStatus.InProgress]: 'amber',
  [JobStatus.AwaitingParts]: 'orange',
  [JobStatus.Completed]: 'green',
  [JobStatus.Invoiced]: 'blue',
}

/** Invoice status is "Active" or "Rejected" (the API has no paid/unpaid concept). */
export const invoiceStatusTone = (status: string): Tone =>
  status.toLowerCase() === 'rejected' ? 'red' : 'green'
