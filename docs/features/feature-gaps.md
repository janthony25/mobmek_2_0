# Feature Gaps — Backlog

Single consolidated list of every gap found across the `docs/features/*.md` audit (2026-10-07), so there's one place to scan for "what's left to fix" instead of reading ten files. Each entry links back to its source file for full evidence/detail.

**Keep this in sync:** when a gap here gets fixed, check it off (or delete the line) in the same change that fixes it, per the `docs/features` update rule in `CLAUDE.md`. When a new gap is discovered, add it here too, not just in the per-feature file.

Checkbox = unresolved. Strike-through + `(fixed <date>)` when closed — keep closed items for a while as a changelog rather than deleting immediately.

## Auth & Staff Management
*(detail: [auth-staff-management.md](auth-staff-management.md))*

- [x] ~~No true logged-out "forgot password" recovery~~ (fixed 2026-10-07) — self-service email-reset flow added (`AuthController.ForgotPassword`/`ResetPassword` + `ForgotPasswordPage.tsx`), anti-enumeration throughout, reuses the existing `PasswordChangeCode` mechanism. 10 new backend tests; live-verified in-browser.
- [x] ~~`EmployeeService.DeleteAsync` has no in-use pre-check~~ (fixed 2026-10-07) — now returns `EmployeeWriteError.InUse` with a friendly 400 when the employee has a login account or is a mechanic on a job; 2 new tests.
- [x] ~~Same gap for `EmployeeTitleService`/`EmploymentTypeService` delete~~ (fixed 2026-10-07) — both now return an `InUse` error with a friendly 400 instead of relying on the DB FK `Restrict` violation; 1 new test each.
- [x] ~~No controller-level/integration test for `AuthController.Login`~~ (fixed 2026-10-07) — `AuthControllerTests.cs` added, 6 tests against a real `UserManager`/`SignInManager` wired the same way as `Program.cs`, covering success, email-not-confirmed, deactivated, generic lockout, wrong password, and unknown email.
- [x] ~~Forwarded-header trust (`ForwardedHeadersOptions`) not confirmed configured~~ (checked 2026-10-07, was never actually a gap) — traced the full chain: Caddy sets `X-Forwarded-Proto`, nginx passes it through via a `map` block, `Program.cs:339-345` translates it with `UseForwardedHeaders`, and the auth cookie's `Secure` flag resolves correctly. Already fixed in a prior session; the original audit just hadn't traced far enough to see it.

## Customers & Vehicles
*(detail: [customers-vehicles.md](customers-vehicles.md))*

- [x] ~~No duplicate-customer detection~~ (fixed 2026-10-07) — soft warning (phone/email), debounced, dismissible, never blocks save. `check-duplicate` endpoint + `CustomerForm.tsx`.
- [x] ~~No duplicate-vehicle detection~~ (fixed 2026-10-07) — hard block (rego always, VIN when provided), only re-checked when the value actually changes (regression-tested against 7 known pre-existing duplicate-rego pairs from the legacy import).
- [~] `car-make-model.md` reference file is dead — never consumed by the seeder, which only has ~10 hardcoded makes. **Decision (2026-10-07): leave as-is** — not wiring it in, not deleting it. Revisit only if the 10-make seed list becomes a real problem in practice.
- [x] ~~`CustomerDetailPage` has no notes panel~~ (fixed 2026-10-07) — `NotesSection.tsx` added (mirrors `RemindersSection.tsx`), backend `GET /api/notes?customerId=` filter added. Live-verified in-browser.
- [x] ~~`Reminder.CarId` "must belong to the same customer" is documented as an invariant but not enforced server-side~~ — **not actually a gap**, corrected 2026-10-07. `ReminderService.ValidateReferencesAsync` already enforces this and has test coverage; the original audit note was wrong.

## Jobs / Workshop Floor
*(detail: [jobs-workshop-floor.md](jobs-workshop-floor.md))*

- [skip] No job-status state machine — any status can transition to any other with zero business-rule enforcement. (Decided 2026-10-07: leaning toward simple guard rails over a full linear state machine when this gets picked up.)
- [skip] No real parts/inventory system — `Product`/`ProductsController` is unused scaffolding; job parts are untracked free text with no stock/supplier/reorder tracking. (Large from-scratch feature — needs its own scoping session.)
- [skip] No real DVI (Digital Vehicle Inspection) feature — `JobPhoto` is a bare upload with no checklist, categorization, annotation, severity, or customer-facing report. (Large from-scratch feature — needs its own scoping session.)
- [ skip] Labour is manual hours entry only — no clock-in/clock-out time tracking.
- [ skip] Reminders have zero delivery mechanism — internal to-do list only, no SMS/email/push trigger of any kind.
- [skip ] Job→Invoice generation is status-independent — an invoice can be created regardless of job status, and `JobStatus.Invoiced` is never auto-set.
- [x] ~~Job photos on local disk storage have no backup coverage~~ — **not actually a gap**, confirmed 2026-10-07. Production is provisioned with `FileStorage:Provider=S3` against a real versioned/encrypted bucket per `infrastructure/README.md`'s deploy checklist (specific bucket name/region/IAM grants, not aspirational). S3 versioning covers accidental-delete recovery; no separate backup script needed for uploads.
- [x] ~~Job photo "Remove" button deleted instantly on click, no confirmation~~ (fixed 2026-10-07) — user-reported; now gated by `ConfirmDialog` in `JobPhotos.tsx`. Triggered an app-wide audit (new standing rule in `mobmek_frontend/CLAUDE.md`: every irreversible delete must go through `ConfirmDialog`) that found two more instances of the same bug — see Business Settings and Cash Flow & GST Reporting below.

## Invoices, Quotes & Public Booking
*(detail: [invoices-quotes-public-booking.md](invoices-quotes-public-booking.md))*

- [x] ~~Invoice `SequenceNumber` generation is not concurrency-safe~~ (fixed 2026-10-07) — unique index on `(DocumentType, SequenceNumber)` + retry-on-conflict in `InvoiceService`. Live-verified generating a real quotation post-fix.
- [x] ~~Two disconnected PDF implementations~~ (fixed 2026-10-07) — `InvoicePrintPage.tsx` deleted; `GET /api/jobs/{jobId}/invoices/{id}/pdf` now serves the real QuestPDF document everywhere (view inline or `?download=true`). Curl-verified both modes return valid PDF bytes with correct headers.
- [skip] Public Booking backend (slot engine, anonymous API, rate-limited, tested) has **zero consuming frontend** anywhere — nobody can actually use it today. (Decided 2026-10-07: real scoping exercise — standalone route vs. separate site, branding, fields to collect — needs its own session.)

## Appointments & Google Calendar Sync
*(detail: [appointments-calendar-sync.md](appointments-calendar-sync.md))*

- [skip] No overlap/double-booking check — two appointments for the same mechanic/time slot can both be created.
- [ skip] No appointment reminders (SMS/email ahead of the appointment) — mechanism doesn't exist at all; only whatever default notification settings exist on the Google account apply.
- [x] ~~Convert-on-arrival wizard (create customer → add car → create job) isn't transactional~~ (fixed 2026-10-07) — the final job-creation step now links the appointment and marks it Arrived in the same `SaveChangesAsync` as job creation (`CreateJobRequest.AppointmentId`), instead of a separate best-effort call after the fact. Steps 1-2 were already atomic individually, and the wizard is resumable by design. 4 new backend tests; live-verified via curl.
- [skip] Conflict handling is overwrite-only — a manual edit made directly in Google Calendar is always silently discarded on the next reconcile, with no surfacing of the conflict. (Decided 2026-10-07: working as designed — the design doc explicitly rejected two-way sync; silent overwrite is intended, not a bug.)

## Cash Flow & GST Reporting [skip]
*(detail: [cash-flow-gst.md](cash-flow-gst.md))*

- [x] ~~Transaction attachment "Remove" button deleted instantly on click, no confirmation~~ (fixed 2026-10-07) — found via the job-photos audit above; now gated by its own `ConfirmDialog` (the transaction delete itself was already correctly gated).
- [ ] No bank statement import (CSV/OFX) — all cash movements are 100% manual entry (plus invoice-payment and recurring auto-posting).
- [ ] No reconciliation feature — `CashTransaction.Status == "Reconciled"` is a dead value nothing ever sets, despite guard checks, UI copy, and a filter option all referencing it. Either build it or stop implying it exists in the UI.
- [ ] No category budgets (`CategoryBudget`) — entirely unbuilt.
- [ ] Forecast is missing a tax-obligation source and a variable-expense run-rate source (4 of the designed 6 sources exist).
- [ ] Forecast scenario multipliers are hardcoded constants, not editable — the "show assumptions" UI implies a settings form that doesn't exist.
- [ ] No forecast-accuracy tracking (`ForecastSnapshot`).
- [ ] No NZ tax-obligation modeling (`TaxProfile`/`TaxObligation` — provisional tax, PAYE, KiwiSaver, ACC deadlines); "Tax Settings" today is just the GST rate.
- [ ] No cash-flow dashboard / Financial Health Score.
- [ ] No reports beyond the GST report (design doc scopes ~12).
- [ ] No AI assistant (design-doc scoped, unbuilt — low priority).

## Email Module
*(detail: [email-module.md](email-module.md))*

- [x] ~~No Resend delivery webhook~~ (fixed 2026-10-07) — `ResendWebhookController` + Svix-style signature verification (`ResendWebhookVerifier`), feeds the same no-regress status state machine as the poll job.
- [x] ~~No email templates~~ (fixed 2026-10-07) — `EmailTemplate` entity (3 seeded keys), `{{Token}}` substitution, editor on the Email Settings page. Invoice send now uses it; reminder/appointment sends (below) use it too.
- [x] ~~No reminder/appointment email triggers~~ (fixed 2026-10-07) — `POST api/reminders/{id}/email` and `POST api/appointments/{id}/email`, new `OutboundEmailKind.Reminder`/`Appointment`, compose modals wired into `ReminderDetailsModal`/`AppointmentDetailModal`.
- [skip] No inbox mirror / IMAP at all — 0% built, not even scaffolded (no entities, no dependency, no job, no UI). (Decided 2026-10-07: needs the `MailKit` dependency signed off first and is large enough to warrant its own session — explicitly out of scope for this round.)
- [ ] No merged customer email timeline (sent + received in one view) — only per-document compose modals exist. The sent half is now buildable (`OutboundEmail.CustomerId` already exists) but wasn't in this round's scope; the received half still needs the inbox mirror.
- [ ] `docs/email-module-todo.md` header still says "Status: Not started" — stale, should be corrected regardless of what else gets fixed. (Left as-is this round — low-priority prose fix, not behavior.)

## Business Settings
*(detail: [business-settings.md](business-settings.md))*

- [x] ~~No configurable invoice/quote number prefix or format~~ (fixed 2026-10-07) — `BusinessDetails.InvoicePrefix`/`QuotePrefix` (default `INV`/`QUO`) now feed `InvoiceService`, `InvoicePdfService`, and `EmailComposeService`; exposed in `BusinessDetailsSettingsPage.tsx`. 1 new backend test + updated existing coverage.
- [x] ~~Logo "Remove" button deleted instantly on click, no confirmation~~ (fixed 2026-10-07) — found via the job-photos audit above; now gated by its own `ConfirmDialog`.

## Legacy Data Import
*(detail: [legacy-data-import.md](legacy-data-import.md))*

- [ ] `docs/legacy-import-{design,todo,testing}.md` are stale — they don't reflect the real import run that happened 2026-08-08 against a newer backup.
- [ ] Phase 6 cutover not done — no final dev-data wipe, no sign-off, `legacy_import_map` table and the `legacy-mssql` container are both still present.

## Infrastructure & Deployment
*(detail: [infrastructure-deployment.md](infrastructure-deployment.md))*

- [ ] No CI/CD pipeline — no `.github/workflows` or any CI config at all; `main` reaches prod via a manual SSH script with no automated build/test gate.
- [ ] No monitoring/alerting/error-tracking — no Sentry/APM/log-aggregation anywhere, no structured logging framework, minimal `ILogger` usage.
- [x] ~~Deploy-mechanism gap caused a real outage~~ (fixed 2026-10-07) — two same-day commits' migrations were never applied to prod after `publish.sh` deployed their code, breaking invoice/quote generation live; fixed by running the documented migration procedure. **New follow-up gap surfaced by this incident:** `publish.sh` has no check for pending migrations before deploying — nothing warns when code has drifted ahead of the live schema.

## UI Shell & Navigation
*(detail: [ui-shell-navigation.md](ui-shell-navigation.md))*

- [x] ~~Mobile/tablet top bar didn't stay pinned; page drag/scroll felt "stuck"~~ (fixed 2026-10-07) — `h-screen` → `h-dvh` in `AppLayout.tsx`; `100vh` exceeded the real visible viewport on mobile Safari/Chrome whenever the address bar was showing, making the whole document a second scroll container instead of just `<main>`.
- [x] ~~Dropdown/combobox menus could get stuck open on mobile~~ (fixed 2026-10-07) — `DropdownMenu`, `Combobox`, `AsyncCombobox`, and `CustomerDetailPage`'s inline date filter now also listen for `touchstart`, since iOS Safari doesn't reliably fire synthetic `mousedown` for taps on non-"clickable" elements.
- [x] ~~List-page header (title + search/toggle/Add) didn't wrap on narrow screens, squeezing the title text~~ (fixed 2026-10-07) — `CrudSection.tsx` header now stacks below `sm` and wraps its controls row.
- [x] ~~Sidebar collapse (desktop icon-rail preference) leaked into the mobile/tablet drawer, hiding all nav labels~~ (fixed 2026-10-07) — user-reported, live on an iPhone. `Sidebar.tsx` nav labels/headings/footer now use the same `lg:hidden`-scoped-class pattern the brand title already used, instead of an unscoped `{!collapsed && ...}` JS conditional.
- [ ] None of the fixes above were live-verified on a real device this session (no browser automation available) — worth a real-device pass, including a tablet per the user's "might be the same for tablets" note. **None of them have been deployed to production yet either** — all are uncommitted local changes as of 2026-10-07.

## Cross-cutting (appear more than once above)
- **No notification/delivery layer beyond outbound invoice email** — job reminders and appointment reminders both have zero SMS/email/push mechanism.
- **CI/CD and observability are both fully absent**, system-wide.
- **Several design/todo docs under `docs/` are stale** in both directions (describe less than what shipped, or more) — always cross-check the matching `docs/features/*.md` file before trusting them.
