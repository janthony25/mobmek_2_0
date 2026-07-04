# Loading indicators — what's covered

Everything below was added/fixed in the same pass. Core fix: `useAsync`'s `reload()`
used to null out data instantly but never flip `loading`, so any page guarded by
`if (!x) return "not found"` (or an empty-list check) could briefly flash that wrong
state after a save/delete/action — anywhere in the app. That's fixed at the hook level
(`src/hooks/useAsync.ts`), plus a shared `Spinner` component and visible spinners were
added on top of it. Review this list for anything still missing.

## Shared building blocks (affect every page below)
- `useAsync` (`src/hooks/useAsync.ts`) — reload/refetch now keeps old data on screen
  and only toggles `loading`, instead of flashing "not found"/empty.
- `Spinner` (`src/components/ui/Spinner.tsx`) — new shared spinner icon.
- `StateMessage` — gained an optional `loading` prop (spinner + text) for full-page/
  full-section loading placeholders.
- `CrudSection` (`src/components/crud/CrudSection.tsx`) — the shared list/table
  abstraction. Now: full placeholder only on the very first fetch; a small spinner
  next to the heading during any background refresh (search, page change, reload
  after mutation); a spinner inside the search box while a search request is in
  flight; per-row actions (`extraAction`) disable themselves and show "Working…"
  while their request is in flight (button and dropdown-menu variants).
- `ConfirmDialog` (delete confirmations) and `ResourceForm` (create/edit modals) —
  their existing "Working…"/"Saving…" busy text now also shows a spinner icon.

## Pages built on `CrudSection` (get all of the above automatically)
- Customers (`CustomersPage`)
- Job Center list (`JobCenterPage`)
- Products (`ProductsPage`)
- Car Makes & Models (`CarMakesPage`)
- Job Services (`JobServicesPage`)
- Employees (`EmployeesPage`)
- Employee Titles / Employment Types (`LookupCrudPage` → `EmployeeTitlesPage`,
  `EmploymentTypesPage`)
- Cash Accounts (`CashAccountsPage`)
- Transaction Categories (`TransactionCategoriesPage`)
- Payees (`PayeesPage`)
- Categorization Rules (`CategorizationRulesPage`)
- Recurring & Planned — the Recurring Schedules and Planned Items tables
  (`RecurringPlannedPage`)
- Reminder Templates (`ReminderTemplatesPage`)
- Invoices page / Quotations page (`DocumentListPage`, shared by both routes —
  includes the date-search button)
- Reminders section embedded on Customer/Car detail pages (`RemindersSection`)

## Job Page
- Job Detail page (`JobDetailPage`) — the top-level job load no longer flashes
  "Job not found" after saving edits (was the clearest instance of the root-cause
  bug: `reloadJob()` after an edit used to momentarily null the job and hit the
  not-found branch).

## Vehicle Details Page
- Car Detail page (`CarDetailPage`) — same "not found" flash fixed for the car
  itself; its embedded Jobs list now shows a spinner alongside "Loading jobs…".

## Customer / Customer Details Page
- Customer Detail page (`CustomerDetailPage`) — "Customer not found" flash fixed
  (this is the one verified live: 10 rapid snapshots right after an edit+save showed
  no flash and no console errors). Its Vehicles, Appointment History, Invoices and
  Quotes inline lists now show a spinner alongside their "Loading …" captions.
- Job Detail page's nested **Invoices** and **Quotations** sections
  (`InvoicesSection`, `QuotationsSection`) — same background-refresh spinner
  treatment as `CrudSection` (generate/mark paid/reject no longer flash the section
  empty).

## Appointment Page
- `AppointmentsPage` — initial load now shows a spinner (was plain text); filter/
  view/date-range changes now show a small spinner next to the range label instead
  of silently refetching with zero feedback.

## General / Settings / Reports (loading-flash fix applied, no visible spinner beyond that)
- Business Details settings (`BusinessDetailsSettingsPage`)
- Tax (GST) settings (`TaxSettingsPage`)
- GST Report (`GstReportPage`) — also dims the existing report slightly while a new
  date range loads, instead of just double-showing a loading caption on top of it
- Forecast (`ForecastPage`)
- Notes & Reminders page (`NotesRemindersPage`) — notes and reminders lists
- Cash Flow ledger (`CashFlowPage`)
- Invoice/Quotation print page (`InvoicePrintPage`) — spinner added, no reload risk
  here (loads once)

## Not touched / worth a second look
- **Route-level navigation** (clicking a sidebar link to a totally different page)
  has no global progress bar — there's no code-splitting in this app (all pages are
  eagerly imported), so there's no JS-loading delay, only each page's own data fetch,
  which is covered above. If that's not enough, a global top-loading-bar would be a
  separate, small addition.
- **Notes/Reminders side panel** (the pinned panel next to every page, not the
  `/notes-reminders` page) uses its own fetch logic outside `CrudSection` — not
  audited in this pass.
- **PDF view/download/print actions** open a new tab/window synchronously
  (`window.open`) — no loading state applicable.
- **Export button** on Cash Flow (CSV export) navigates via
  `window.location.assign` — not covered, could use a busy indicator if exports are
  large.
- Any page not listed above either has no data fetching (static) or wasn't part of
  this pass — flag it and we can extend the same pattern.
