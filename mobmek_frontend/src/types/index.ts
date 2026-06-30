// Domain types mirroring the Mobmek API DTOs.

export interface Customer {
  id: string
  firstName: string
  lastName: string
  phoneNumber: string
  emailAddress: string | null
  physicalAddress: string | null
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

// Mirrors the API's JobStatus enum (serialized as a number).
export const JobStatus = {
  Open: 0,
  InProgress: 1,
  AwaitingParts: 2,
  Completed: 3,
  Invoiced: 4,
} as const

export type JobStatus = (typeof JobStatus)[keyof typeof JobStatus]

export const JOB_STATUS_LABELS: Record<JobStatus, string> = {
  [JobStatus.Open]: 'Open',
  [JobStatus.InProgress]: 'In progress',
  [JobStatus.AwaitingParts]: 'Awaiting parts',
  [JobStatus.Completed]: 'Completed',
  [JobStatus.Invoiced]: 'Invoiced',
}

export interface JobMechanic {
  employeeId: string
  fullName: string
}

export interface Job {
  id: string
  customerId: string
  customerName: string | null
  carId: string
  carDescription: string | null
  title: string
  status: JobStatus
  odometer: number
  jobNotes: string | null
  invoiceNotes: string | null
  totalJobPrice: number
  totalJobProfit: number
  mechanics: JobMechanic[]
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CarMake {
  id: string
  name: string
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CarModel {
  id: string
  carMakeId: string
  carMakeName: string | null
  name: string
  createdAtUtc: string
  updatedAtUtc: string | null
}
