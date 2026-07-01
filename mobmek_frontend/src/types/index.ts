// Domain types mirroring the Mobmek API DTOs.
//
// Enums are expressed as `const` objects + union types because the project's
// TypeScript config enables `erasableSyntaxOnly`, which forbids `enum`.

// --- Customers ---------------------------------------------------------------

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

export interface CustomerRequest {
  firstName: string
  lastName: string
  phoneNumber: string
  emailAddress: string | null
  physicalAddress: string | null
  notes: string | null
}

// --- Cars --------------------------------------------------------------------

export interface Car {
  id: string
  customerId: string
  carMakeId: string
  carMakeName: string | null
  carModelId: string
  carModelName: string | null
  year: number
  rego: string
  vin: string | null
  color: string | null
  engineType: string | null
  odometer: number | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreateCarRequest {
  customerId: string
  carMakeId: string
  carModelId: string
  year: number
  rego: string
  vin: string | null
  color: string | null
  engineType: string | null
  odometer: number | null
}

export type UpdateCarRequest = Omit<CreateCarRequest, 'customerId'>

// --- Car makes & models ------------------------------------------------------

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

// --- Jobs --------------------------------------------------------------------

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

export interface CreateJobRequest {
  customerId: string
  carId: string
  title: string
  status: JobStatus
  odometer: number
  jobNotes: string | null
  invoiceNotes: string | null
}

export type UpdateJobRequest = Omit<CreateJobRequest, 'customerId'>

// --- Job items ---------------------------------------------------------------

export const MarkupSolution = {
  Percentage: 0,
  Dollar: 1,
} as const

export type MarkupSolution = (typeof MarkupSolution)[keyof typeof MarkupSolution]

export const MARKUP_SOLUTION_LABELS: Record<MarkupSolution, string> = {
  [MarkupSolution.Percentage]: 'Percentage',
  [MarkupSolution.Dollar]: 'Dollar',
}

export interface JobItem {
  id: string
  jobId: string
  itemName: string
  tradePrice: number | null
  retailPrice: number | null
  markupSolution: MarkupSolution
  markup: number
  itemQuantity: number
  sellingPrice: number
  unitProfit: number
  itemTotal: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface JobItemRequest {
  itemName: string
  tradePrice: number | null
  retailPrice: number | null
  markupSolution: MarkupSolution
  markup: number
  itemQuantity: number
  sellingPrice: number | null
}

// --- Labour ------------------------------------------------------------------

export interface Labour {
  id: string
  jobId: string
  hours: number | null
  ratePerHour: number | null
  fixedAmount: number | null
  totalAmount: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface LabourRequest {
  hours: number | null
  ratePerHour: number | null
  fixedAmount: number | null
}

// --- Catalog services & job service lines ------------------------------------

export interface JobService {
  id: string
  name: string
  description: string | null
  price: number
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface JobServiceRequest {
  name: string
  description: string | null
  price: number
  isActive: boolean
}

export interface JobServiceLine {
  id: string
  jobId: string
  jobServiceId: string
  serviceName: string | null
  unitPrice: number
  quantity: number
  lineTotal: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreateJobServiceLineRequest {
  jobServiceId: string
  quantity: number
}

// --- Invoices ----------------------------------------------------------------

export interface InvoiceItem {
  id: string
  invoiceId: string
  itemName: string
  quantity: number
  itemPrice: number
  itemTotal: number
}

export interface Invoice {
  id: string
  jobId: string
  issueName: string
  notes: string | null
  documentType: string
  /** "Active" or "Rejected". */
  status: string
  dueDate: string | null
  /** Set when the invoice is marked paid — not known at generation time. */
  paymentTerm: string | null
  modeOfPayment: string | null
  labourPrice: number
  subTotal: number
  /** GST rate applied, as a fraction (0.15 = 15%). */
  gstRate: number
  taxAmount: number
  discount: number
  shippingFee: number
  totalAmount: number
  isPaid: boolean
  amountPaid: number | null
  datePaid: string | null
  cashAmount: number | null
  cardAmount: number | null
  items: InvoiceItem[]
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreateInvoiceRequest {
  dueDate: string | null
}

export interface MarkInvoicePaidRequest {
  modeOfPayment: string | null
  paymentTerm: string | null
  cashAmount: number | null
  cardAmount: number | null
  datePaid: string | null
}

// --- GST setting -------------------------------------------------------------

export interface GstSetting {
  id: string
  /** Rate as a fraction (0.15 = 15%). */
  rate: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface UpdateGstSettingRequest {
  rate: number
}

// --- Products ----------------------------------------------------------------

export interface Product {
  id: string
  name: string
  description: string | null
  price: number
  stockQuantity: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface ProductRequest {
  name: string
  description: string | null
  price: number
  stockQuantity: number
}

// --- Employees & lookups -----------------------------------------------------

export interface Employee {
  id: string
  firstName: string
  lastName: string
  titleId: string
  titleName: string | null
  employmentTypeId: string
  employmentTypeName: string | null
  contactNumber: string
  emailAddress: string
  physicalAddress: string
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface EmployeeRequest {
  firstName: string
  lastName: string
  titleId: string
  employmentTypeId: string
  contactNumber: string
  emailAddress: string
  physicalAddress: string
}

export interface EmployeeTitle {
  id: string
  name: string
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface EmploymentType {
  id: string
  name: string
  createdAtUtc: string
  updatedAtUtc: string | null
}

/** Shared shape for the many "name only" lookup entities. */
export interface NamedLookupRequest {
  name: string
}

// --- Notes (sticky board) ----------------------------------------------------

export interface Note {
  id: string
  title: string
  body: string | null
  /** Optional due date, "yyyy-mm-dd"; lets the board flag notes as due soon. */
  dueDate: string | null
  /** Palette key (see NOTE_COLORS); null falls back to the default sticky colour. */
  color: string | null
  isPinned: boolean
  isDone: boolean
  customerId: string | null
  customerName: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface NoteRequest {
  title: string
  body: string | null
  dueDate: string | null
  color: string | null
  isPinned: boolean
  isDone: boolean
  customerId: string | null
}

// --- Reminder templates (reusable presets) -----------------------------------

export interface ReminderTemplate {
  id: string
  name: string
  description: string | null
  /** Optional gap in months used to pre-fill a reminder's due date. */
  defaultIntervalMonths: number | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface ReminderTemplateRequest {
  name: string
  description: string | null
  defaultIntervalMonths: number | null
}

// --- Reminders (dated, customer/car) -----------------------------------------

export interface Reminder {
  id: string
  customerId: string
  customerName: string
  carId: string | null
  carLabel: string | null
  reminderTemplateId: string | null
  reminderTemplateName: string | null
  title: string
  /** Calendar date, "yyyy-mm-dd". */
  dueDate: string
  isDone: boolean
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreateReminderRequest {
  customerId: string
  carId: string | null
  reminderTemplateId: string | null
  title: string
  dueDate: string
  isDone: boolean
  notes: string | null
}

export type UpdateReminderRequest = Omit<CreateReminderRequest, 'customerId'>
