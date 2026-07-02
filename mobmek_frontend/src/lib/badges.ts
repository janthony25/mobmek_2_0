import type { Tone } from '@/components/ui/Badge'
import { JobStatus } from '@/types'
import type { Invoice } from '@/types'

/** Colour tone per job status, for the appointment-history status badges. */
export const JOB_STATUS_TONE: Record<JobStatus, Tone> = {
  [JobStatus.Open]: 'slate',
  [JobStatus.InProgress]: 'amber',
  [JobStatus.AwaitingParts]: 'orange',
  [JobStatus.Completed]: 'green',
  [JobStatus.Invoiced]: 'blue',
}

/**
 * An invoice's `status` field is only "Active" or "Rejected"; whether an active invoice has
 * been paid is a separate `isPaid` flag, so the display label/tone combines both.
 */
export const invoiceStatusLabel = (invoice: Pick<Invoice, 'status' | 'isPaid'>): string =>
  invoice.status.toLowerCase() === 'rejected' ? 'Rejected' : invoice.isPaid ? 'Paid' : 'Unpaid'

export const invoiceStatusTone = (invoice: Pick<Invoice, 'status' | 'isPaid'>): Tone =>
  invoice.status.toLowerCase() === 'rejected' ? 'red' : invoice.isPaid ? 'green' : 'amber'

/** A quotation is never paid, so its label/tone come from `status` alone. */
export const quotationStatusLabel = (quotation: Pick<Invoice, 'status'>): string =>
  quotation.status.toLowerCase() === 'rejected' ? 'Rejected' : 'Active'

export const quotationStatusTone = (quotation: Pick<Invoice, 'status'>): Tone =>
  quotation.status.toLowerCase() === 'rejected' ? 'red' : 'blue'
