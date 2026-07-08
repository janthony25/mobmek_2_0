// Domain types mirroring the Mobmek API DTOs.
//
// Enums are expressed as `const` objects + union types because the project's
// TypeScript config enables `erasableSyntaxOnly`, which forbids `enum`.

// --- Shared ------------------------------------------------------------------

/** One page of a server-paginated list plus the total row count. */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

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

/** A customer's car as shown on the customer list, with its active-reminder info. */
export interface CustomerCarSummary {
  id: string
  year: number
  carMakeName: string | null
  carModelName: string | null
  activeReminderCount: number
  /** Earliest active reminder due date, "yyyy-mm-dd". */
  nextReminderDueDate: string | null
}

/** Customer list-page shape: the customer plus the aggregates the cards display. */
export interface CustomerListItem extends Customer {
  cars: CustomerCarSummary[]
  activeNoteCount: number
  /** Earliest active note due date, "yyyy-mm-dd". */
  nextNoteDueDate: string | null
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

/** Tinted pill (background + text) for the status badge on the job card. */
export const JOB_STATUS_COLORS: Record<JobStatus, string> = {
  [JobStatus.Open]: 'bg-slate-100 text-slate-600',
  [JobStatus.InProgress]: 'bg-amber-50 text-amber-700',
  [JobStatus.AwaitingParts]: 'bg-orange-50 text-orange-700',
  [JobStatus.Completed]: 'bg-green-50 text-green-600',
  [JobStatus.Invoiced]: 'bg-blue-50 text-blue-600',
}

export interface JobMechanic {
  employeeId: string
  fullName: string
}

export const DiscountType = {
  None: 0,
  Fixed: 1,
  Percentage: 2,
} as const

export type DiscountType = (typeof DiscountType)[keyof typeof DiscountType]

export const DISCOUNT_TYPE_LABELS: Record<DiscountType, string> = {
  [DiscountType.None]: 'None',
  [DiscountType.Fixed]: '$ Fixed amount',
  [DiscountType.Percentage]: '% Percentage',
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
  discountType: DiscountType
  discountValue: number
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
  discountType: DiscountType
  discountValue: number
}

export type UpdateJobRequest = Omit<CreateJobRequest, 'customerId'>

// --- Appointments --------------------------------------------------------------

export const AppointmentStatus = {
  Scheduled: 0,
  Confirmed: 1,
  Arrived: 2,
  Completed: 3,
  NoShow: 4,
  Cancelled: 5,
} as const

export type AppointmentStatus = (typeof AppointmentStatus)[keyof typeof AppointmentStatus]

export const APPOINTMENT_STATUS_LABELS: Record<AppointmentStatus, string> = {
  [AppointmentStatus.Scheduled]: 'Scheduled',
  [AppointmentStatus.Confirmed]: 'Confirmed',
  [AppointmentStatus.Arrived]: 'Arrived',
  [AppointmentStatus.Completed]: 'Completed',
  [AppointmentStatus.NoShow]: 'No-show',
  [AppointmentStatus.Cancelled]: 'Cancelled',
}

/**
 * A booked visit. Either fully linked to customer/car (and optionally a job), or a
 * "new caller" carrying only free-text contact fields until converted at check-in.
 */
export interface Appointment {
  id: string
  title: string
  startUtc: string
  endUtc: string
  status: AppointmentStatus
  notes: string | null
  contactName: string | null
  contactPhone: string | null
  vehicleDescription: string | null
  customerId: string | null
  customerName: string | null
  carId: string | null
  carDescription: string | null
  jobId: string | null
  jobTitle: string | null
  mechanicId: string | null
  mechanicName: string | null
  googleEventId: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreateAppointmentRequest {
  title: string
  startUtc: string
  endUtc: string
  status: AppointmentStatus
  notes: string | null
  contactName: string | null
  contactPhone: string | null
  vehicleDescription: string | null
  customerId: string | null
  carId: string | null
  jobId: string | null
  mechanicId: string | null
}

export type UpdateAppointmentRequest = CreateAppointmentRequest

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
  invoiceNumber: string
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

export interface AcceptQuotationRequest {
  dueDate: string | null
}

export interface MarkInvoicePaidRequest {
  modeOfPayment: string | null
  paymentTerm: string | null
  cashAmount: number | null
  cardAmount: number | null
  datePaid: string | null
}

/** One row of the global Invoices/Quotations list — an invoice plus its job/customer/vehicle context. */
export interface InvoiceListItem {
  id: string
  jobId: string
  invoiceNumber: string
  issueName: string
  documentType: string
  /** "Active", "Rejected", or (quotations only) "Accepted". */
  status: string
  customerName: string | null
  carDescription: string | null
  dueDate: string | null
  totalAmount: number
  isPaid: boolean
  createdAtUtc: string
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

// --- Business details (letterhead) -------------------------------------------

export interface BusinessDetails {
  id: string
  name: string
  address: string | null
  email: string | null
  businessPhone: string | null
  telephone: string | null
  /** GST registration number, shown on generated invoices. */
  gstNumber: string | null
  website: string | null
  /** Free-text bank/payment details shown on invoices for bank-transfer payers. */
  bankDetails: string | null
  logoUrl: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface UpdateBusinessDetailsRequest {
  name: string
  address: string | null
  email: string | null
  businessPhone: string | null
  telephone: string | null
  gstNumber: string | null
  website: string | null
  bankDetails: string | null
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
  /** When the note was last marked done (ISO timestamp); null while open. */
  doneAtUtc: string | null
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

// --- Cash flow ----------------------------------------------------------------

export interface CashAccount {
  id: string
  name: string
  /** "Bank", "Cash", "DigitalWallet" or "CreditCard". */
  type: string
  accountNumber: string | null
  openingBalance: number
  /** Calendar date, "yyyy-mm-dd" — the ledger for this account starts here. */
  openingDate: string
  isArchived: boolean
  /** Derived on the backend: opening balance + inflows − outflows. */
  currentBalance: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CashAccountRequest {
  name: string
  type: string
  accountNumber: string | null
  openingBalance: number
  openingDate: string
  isArchived?: boolean
}

export const CASH_ACCOUNT_TYPES = ['Bank', 'Cash', 'DigitalWallet', 'CreditCard'] as const

export const CASH_ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Bank: 'Bank account',
  Cash: 'Cash / till',
  DigitalWallet: 'Digital wallet',
  CreditCard: 'Credit card',
}

export interface TransactionCategory {
  id: string
  name: string
  /** "In", "Out" or "Either". */
  direction: string
  group: string
  /** Seeded system categories are rename/archive-only. */
  isSystem: boolean
  /** "Taxable", "Exempt" or "ZeroRated". */
  defaultGstTreatment: string
  excludeFromOperatingExpense: boolean
  isArchived: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface TransactionCategoryRequest {
  name: string
  direction: string
  group: string
  defaultGstTreatment: string | null
  excludeFromOperatingExpense: boolean
  isArchived?: boolean
}

export interface TransactionAttachment {
  id: string
  cashTransactionId: string
  fileName: string
  contentType: string
  sizeBytes: number
  createdAtUtc: string
}

export const TRANSACTION_STATUSES = ['Pending', 'Cleared', 'Reconciled'] as const

export const TRANSACTION_STATUS_LABELS: Record<string, string> = {
  Pending: 'Pending',
  Cleared: 'Cleared',
  Reconciled: 'Reconciled',
}

export interface CashTransaction {
  id: string
  accountId: string
  accountName: string
  /** "In" or "Out". */
  direction: string
  /** Always positive; the direction carries the sign. GST-inclusive. */
  amount: number
  /** Calendar date, "yyyy-mm-dd" — when the cash actually moved. */
  date: string
  description: string
  categoryId: string
  categoryName: string
  /** Optional link to a normalized payee; counterparty holds the display text. */
  payeeId: string | null
  counterparty: string | null
  /** "Pending", "Cleared" or "Reconciled" (reconciled rows are immutable). */
  status: string
  /** Set when auto-posted from an invoice payment — read-only in the ledger. */
  invoiceId: string | null
  /** The invoice's job, for jumping from a ledger row to its source. */
  jobId: string | null
  /** Set on the two paired legs of a transfer — managed together. */
  transferGroupId: string | null
  /** Set on the sibling lines of a split payment — edited/deleted as a group. */
  splitGroupId: string | null
  /** "Taxable", "Exempt" or "ZeroRated". */
  gstTreatment: string
  notes: string | null
  attachments: TransactionAttachment[]
  createdAtUtc: string
  updatedAtUtc: string | null
  /** Account balance after this row; only on single-account unthinned views. */
  runningBalance: number | null
}

export interface CashTransactionPage {
  items: CashTransaction[]
  page: number
  pageSize: number
  totalCount: number
  /** Filter-wide totals (not just this page); transfer legs excluded. */
  totalIn: number
  totalOut: number
}

export interface CashTransactionRequest {
  accountId: string
  direction: string
  amount: number
  date: string
  description: string
  categoryId: string
  /** When set, the payee's name becomes the counterparty text. */
  payeeId: string | null
  counterparty: string | null
  /** Omit (null) to use the category's default. */
  gstTreatment: string | null
  /** "Pending" or "Cleared"; null = Cleared on create / keep current on update. */
  status: string | null
  notes: string | null
}

export interface CreateTransferRequest {
  fromAccountId: string
  toAccountId: string
  amount: number
  date: string
  description: string | null
  notes: string | null
}

export interface SplitTransactionLine {
  amount: number
  categoryId: string
  gstTreatment: string | null
  /** Line-specific text; null uses the split's shared description. */
  description: string | null
}

export interface SplitTransactionRequest {
  accountId: string
  direction: string
  date: string
  description: string
  payeeId: string | null
  counterparty: string | null
  status: string | null
  notes: string | null
  /** At least two lines. */
  lines: SplitTransactionLine[]
}

export type BulkTransactionAction = 'SetCategory' | 'SetStatus' | 'Delete'

export interface BulkTransactionRequest {
  ids: string[]
  action: BulkTransactionAction
  categoryId: string | null
  status: string | null
}

export interface BulkSkippedRow {
  id: string
  reason: string
}

export interface BulkTransactionResult {
  updatedCount: number
  skipped: BulkSkippedRow[]
}

// --- Payees & categorization rules -----------------------------------------------

export interface Payee {
  id: string
  name: string
  defaultCategoryId: string | null
  defaultCategoryName: string | null
  defaultGstTreatment: string | null
  notes: string | null
  isArchived: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface PayeeRequest {
  name: string
  defaultCategoryId: string | null
  defaultGstTreatment: string | null
  notes: string | null
  isArchived?: boolean
}

export interface PayeeSummary {
  id: string
  name: string
  transactionCount: number
  firstDate: string | null
  lastDate: string | null
  totalIn12Months: number
  totalOut12Months: number
}

export const RULE_MATCH_FIELDS = ['Either', 'Description', 'Counterparty'] as const

export const RULE_MATCH_FIELD_LABELS: Record<string, string> = {
  Either: 'Description or counterparty',
  Description: 'Description',
  Counterparty: 'Counterparty',
}

export const RULE_MATCH_TYPES = ['Contains', 'StartsWith', 'Equals'] as const

export const RULE_MATCH_TYPE_LABELS: Record<string, string> = {
  Contains: 'Contains',
  StartsWith: 'Starts with',
  Equals: 'Equals',
}

export interface CategorizationRule {
  id: string
  name: string
  /** Lowest priority wins when several rules match. */
  priority: number
  matchField: string
  matchType: string
  matchValue: string
  direction: string | null
  amountMin: number | null
  amountMax: number | null
  setCategoryId: string
  setCategoryName: string
  setGstTreatment: string | null
  setPayeeId: string | null
  setPayeeName: string | null
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CategorizationRuleRequest {
  name: string
  priority: number
  matchField: string
  matchType: string
  matchValue: string
  direction: string | null
  amountMin: number | null
  amountMax: number | null
  setCategoryId: string
  setGstTreatment: string | null
  setPayeeId: string | null
  isActive: boolean
}

export interface RuleSuggestion {
  ruleId: string
  ruleName: string
  categoryId: string
  categoryName: string
  gstTreatment: string | null
  payeeId: string | null
  payeeName: string | null
}

export interface ApplyRuleResult {
  matchCount: number
  updatedCount: number
}

// --- Audit trail ------------------------------------------------------------------

export interface AuditFieldChange {
  field: string
  old: string | null
  new: string | null
}

export interface CashFlowAuditLog {
  id: string
  entityType: string
  entityId: string
  /** "Created", "Updated" or "Deleted". */
  action: string
  summary: string
  changes: AuditFieldChange[] | null
  timestampUtc: string
}

export interface CashFlowAuditPage {
  items: CashFlowAuditLog[]
  page: number
  pageSize: number
  totalCount: number
}

export interface CashFlowSettings {
  id: string
  defaultAccountId: string | null
  cashAccountId: string | null
  cardAccountId: string | null
  bankTransferAccountId: string | null
  /** Minimum cash the owner wants kept; drives the forecast's shortage detection. */
  safetyBufferAmount: number
  /** Transactions dated on/before this are locked against changes. */
  lockDate: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface UpdateCashFlowSettingsRequest {
  defaultAccountId: string | null
  cashAccountId: string | null
  cardAccountId: string | null
  bankTransferAccountId: string | null
  safetyBufferAmount: number
  lockDate: string | null
}

// --- Recurring & planned transactions -------------------------------------------

export const RECURRING_FREQUENCIES = ['Weekly', 'Fortnightly', 'Monthly', 'Quarterly', 'Annually'] as const

export const RECURRING_FREQUENCY_LABELS: Record<string, string> = {
  Weekly: 'Weekly',
  Fortnightly: 'Fortnightly',
  Monthly: 'Monthly',
  Quarterly: 'Quarterly',
  Annually: 'Annually',
}

export interface RecurringTransaction {
  id: string
  description: string
  /** "In" or "Out". */
  direction: string
  amount: number
  categoryId: string
  categoryName: string
  accountId: string
  accountName: string
  counterparty: string | null
  /** "Taxable", "Exempt" or "ZeroRated". */
  gstTreatment: string
  /** "Weekly", "Fortnightly", "Monthly", "Quarterly" or "Annually". */
  frequency: string
  interval: number
  /** Calendar date, "yyyy-mm-dd" — the first occurrence. */
  anchorDate: string
  endDate: string | null
  autoPost: boolean
  isPaused: boolean
  /** Computed: earliest un-posted occurrence, or null when paused/exhausted. */
  nextOccurrenceDate: string | null
  /** Computed: amount normalised to a per-month cost. */
  monthlyEquivalentAmount: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface RecurringTransactionRequest {
  description: string
  direction: string
  amount: number
  categoryId: string
  accountId: string
  counterparty: string | null
  gstTreatment: string | null
  frequency: string
  interval: number
  anchorDate: string
  endDate: string | null
  autoPost: boolean
  /** Only read by update; create always starts unpaused. */
  isPaused?: boolean
}

export interface DueOccurrence {
  recurringTransactionId: string
  description: string
  direction: string
  amount: number
  accountId: string
  accountName: string
  date: string
}

export const PLANNED_TRANSACTION_SCENARIO_TAGS = ['BestCase', 'WorstCase'] as const

export const PLANNED_TRANSACTION_SCENARIO_LABELS: Record<string, string> = {
  BestCase: 'Best case only',
  WorstCase: 'Worst case only',
}

export const PLANNED_TRANSACTION_STATUSES = ['Planned', 'Posted', 'Cancelled'] as const

export interface PlannedTransaction {
  id: string
  description: string
  /** "In" or "Out". */
  direction: string
  amount: number
  /** Calendar date, "yyyy-mm-dd". */
  expectedDate: string
  categoryId: string
  categoryName: string
  accountId: string | null
  accountName: string | null
  /** "Planned" (editable) → "Posted" or "Cancelled" (terminal). */
  status: string
  /** Null = included in every scenario ("Always"); otherwise confines it to "BestCase" or "WorstCase". */
  scenarioTag: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreatePlannedTransactionRequest {
  description: string
  direction: string
  amount: number
  expectedDate: string
  categoryId: string
  accountId: string | null
  scenarioTag: string | null
}

export interface UpdatePlannedTransactionRequest extends CreatePlannedTransactionRequest {
  status: string
}

// --- Forecast --------------------------------------------------------------------

export const FORECAST_SCENARIOS = ['BestCase', 'Expected', 'WorstCase'] as const

export const FORECAST_SCENARIO_LABELS: Record<string, string> = {
  BestCase: 'Best case',
  Expected: 'Expected',
  WorstCase: 'Worst case',
}

export interface ForecastPoint {
  date: string
  openingBalance: number
  in: number
  out: number
  closingBalance: number
}

export interface ForecastMonthPoint {
  year: number
  month: number
  in: number
  out: number
  closingBalance: number
}

export interface ForecastResult {
  horizonDays: number
  scenario: string
  openingBalance: number
  dailyPoints: ForecastPoint[]
  monthlyPoints: ForecastMonthPoint[]
  shortageDate: string | null
}

export interface GstScopeTotals {
  gstOnSales: number
  gstOnPurchases: number
  netGst: number
}

// Review-only figures: "included" covers every account, "excluded" leaves out Cash-type
// accounts. Neither changes what's actually filed/remitted.
export interface GstReport {
  periodStart: string
  periodEnd: string
  included: GstScopeTotals
  excluded: GstScopeTotals
  cashGst: number
}

// --- Auth ----------------------------------------------------------------------

export interface CurrentUser {
  id: string
  email: string
  employeeId: string
  firstName: string
  lastName: string
  roles: string[]
}

export interface LoginRequest {
  email: string
  password: string
}
