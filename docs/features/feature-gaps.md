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
- [x] ~~`car-make-model.md` reference file is dead~~ (fixed 2026-10-12) — merged into `CarMakes`/`CarModels` directly via a generated idempotent SQL script (case-insensitive find-or-create, same semantics as `MakeModelResolver`), run on local then EC2. 31→73 makes, 211→1319 models on both, zero case-insensitive duplicates, purely additive.
- [x] ~~`CustomerDetailPage` has no notes panel~~ (fixed 2026-10-07) — `NotesSection.tsx` added (mirrors `RemindersSection.tsx`), backend `GET /api/notes?customerId=` filter added. Live-verified in-browser.
- [x] ~~`Reminder.CarId` "must belong to the same customer" is documented as an invariant but not enforced server-side~~ — **not actually a gap**, corrected 2026-10-07. `ReminderService.ValidateReferencesAsync` already enforces this and has test coverage; the original audit note was wrong.
- [x] ~~Customers could be created with zero cars, weakening the rego-uniqueness guard against duplicates~~ (fixed 2026-10-11) — `POST /api/customers/with-car` creates customer+car atomically (nothing saved on car-side validation failure, not even the customer); `CustomerForm.tsx` requires a full car when creating. 3 new tests incl. the atomicity regression.
- [ ] New "Add Customer + Car" flow (above) not live-browser-verified — local Postgres now mirrors a real EC2 snapshot with no known admin password; relying on automated test coverage until a real login is available.

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
- [ ] Partial payments don't post to the Cash Flow ledger (added 2026-10-13) — `InvoiceService.AddPartialPaymentAsync` is record-keeping only (amount/mode/date/notes, reduces `RemainingBalance`); no `CashTransaction` is created, so Cash Flow won't show the money until the invoice is eventually marked fully paid in one lump via "Mark as Paid" (which still posts the *full* `TotalAmount`, unchanged). Deliberately deferred — scoped down from an earlier over-engineered attempt that tried to post each partial payment as its own ledger entry and settle only the remaining delta on final payment; reverted to keep the first cut simple. Revisit when ledger accuracy for partial payments matters.
- [x] ~~Not live-browser-verified~~ (verified 2026-10-13, by the user directly — no browser extension was available this session) — surfaced several real issues in the first cut, all fixed same day: (1) payment-history text leaking into `Invoice.Notes`/job notes and showing on the generated PDF; (2) the history list wired into the wrong component (`JobForm.tsx`, a secondary edit surface) instead of `JobDetailPage.tsx`, the page actually used, so nothing visibly changed after recording a payment; (3) a "Paid" banner amount that double-counted partial payments; (4) a "Payment History" sub-heading the user didn't want (now plain lines after Notes, opt-in per payment via a "Show on invoice" toggle). All fixed; PDF rendering itself still not pypdf-verified against real extracted text.
- [ ] "Paid" banner disabled on the generated PDF (added 2026-10-13) — `InvoicePdfService`'s `showPaidBanner` const is hardcoded `false`, per the user's explicit request. Reason given: some invoices were paid long ago but are still unmarked `IsPaid` in the system, and there's a concern about accuracy if those get marked paid now (Mark Paid's "Date paid" field already supports backdating, `MarkPaidForm.tsx:91-92` — the exact remaining worry wasn't specified beyond "could have a problem with payment date"). Re-enable by flipping `showPaidBanner` to `true` once that concern is resolved.
- [x] ~~Xero invoice push (Phase 0+1) not live-verified~~ (fixed/verified 2026-10-17) — both phases now live-verified against the real "Mobile Mekaniko" Xero org, including a real pushed test invoice (INV-0671). Found and fixed a real bug along the way: Xero requires a `DueDate` on every invoice, which Mobmek allows leaving unset — `XeroSyncJob` now defaults to the issue date when unset. See `xero-integration.md`. Phase 2 (two-way payment sync, webhook + Journals-API reconcile) not started — see `docs/xero-integration-design.md`/`-todo.md`.

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
- [ ] No bank-import/reconciliation progress since this was written (added 2026-10-17) — explicitly revisit before building this phase: `docs/xero-integration-design.md` §0 recommends leaning on Xero for reconciliation instead of duplicating it in-house, pending a few months of real Xero usage.

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

- [x] ~~Phase 6 cutover not done~~ (done 2026-10-11) — EC2 dummy data wiped, fresh `.bak` imported (18/18 reconciliation ✅), data shipped to EC2. Found/fixed a real bug in the process: `AddInvoiceSequenceNumberUniqueIndex` (2026-10-06) broke the importer's placeholder-zero sequence numbers — fixed with a partial index.
- [ ] `docs/legacy-import-{design,todo,testing}.md` are stale — still frozen at 2026-07-10 content, not rewritten even after the 2026-08-08 and 2026-10-11 real runs.
- [ ] `legacy_import_map` table and the `legacy-mssql` compose service/importer project were not torn down after the 2026-10-11 cutover — left in place pending a deliberate cleanup pass.
- [ ] 34 of 37 suspected-duplicate customers + 6 duplicate-rego cars from the 2026-10-11 import are still flagged, not reviewed/merged (owner task, see `legacy-import-report-20261011-080122.md`). 3 merged on EC2 so far: Joseph Gilliard (`#2468`/`#2469`), Aaron (`#2159`/`#2278`), Jhovan/Jovan (`#2372`/`#2376`) — tracked in `docs/legacy-import-duplicate-customers.md`.
- [x] ~~`dotnet test` suite is 100% EF InMemory, can't catch relational-only bugs like partial unique indexes~~ (fixed 2026-10-11) — added `tests/MobmekApi.Tests/Data/InvoiceSequenceIndexTests.cs`, the first real-Postgres test in the suite (needs `docker compose up -d db`), pinning the exact `AddInvoiceSequenceNumberUniqueIndex`/partial-index behavior above.
- [x] ~~`car-make-model.md` not yet merged into `CarMakes`/`CarModels`~~ (fixed 2026-10-12) — see `customers-vehicles.md`/the Customers & Vehicles section above for detail.

## Infrastructure & Deployment
*(detail: [infrastructure-deployment.md](infrastructure-deployment.md))*

- [x] ~~No CI/CD pipeline — no `.github/workflows` or any CI config at all~~ (fixed 2026-10-11) — added `.github/workflows/ci.yml`: build+test on every push to `main` (backend against a real Postgres service container, frontend build+lint), deliberately push-based not PR-gated since this repo has never used branches/PRs. Verified against a from-scratch Postgres before committing, not just the pre-migrated dev DB.
- [ ] No monitoring/alerting/error-tracking — no Sentry/APM/log-aggregation anywhere, no structured logging framework, minimal `ILogger` usage.
- [x] ~~Deploy-mechanism gap caused a real outage~~ (fixed 2026-10-07) — two same-day commits' migrations were never applied to prod after `deploy.sh` deployed their code, breaking invoice/quote generation live; fixed by running the documented migration procedure.
- [x] ~~`deploy.sh` has no check for pending migrations before deploying~~ (fixed 2026-10-11) — after `git pull` on the box, diffs the migration files in the pulled commit against the box's own `__EFMigrationsHistory` (over the same SSH session, no new tooling needed) and fails loudly before rebuilding containers if anything's missing. Verified both ways: ran for real against EC2 (correctly found zero pending) and against synthetic data (correctly flagged a deliberately-missing migration).
- [x] ~~Pre-migration backup was only a documented recommendation, not automatic~~ (fixed 2026-10-12) — new `scripts/apply-migration.sh` always takes a `pg_dump` before applying `migrate.sql`, aborting if it looks suspiciously small, replacing the old fully-manual `scp`/`ssh`/`pg_dump`/`psql` sequence.

## UI Shell & Navigation
*(detail: [ui-shell-navigation.md](ui-shell-navigation.md))*

- [x] ~~Mobile/tablet top bar didn't stay pinned; page drag/scroll felt "stuck"~~ (fixed 2026-10-07) — `h-screen` → `h-dvh` in `AppLayout.tsx`; `100vh` exceeded the real visible viewport on mobile Safari/Chrome whenever the address bar was showing, making the whole document a second scroll container instead of just `<main>`.
- [x] ~~Dropdown/combobox menus could get stuck open on mobile~~ (fixed 2026-10-07) — `DropdownMenu`, `Combobox`, `AsyncCombobox`, and `CustomerDetailPage`'s inline date filter now also listen for `touchstart`, since iOS Safari doesn't reliably fire synthetic `mousedown` for taps on non-"clickable" elements.
- [x] ~~List-page header (title + search/toggle/Add) didn't wrap on narrow screens, squeezing the title text~~ (fixed 2026-10-07) — `CrudSection.tsx` header now stacks below `sm` and wraps its controls row.
- [x] ~~Sidebar collapse (desktop icon-rail preference) leaked into the mobile/tablet drawer, hiding all nav labels~~ (fixed 2026-10-07) — user-reported, live on an iPhone. `Sidebar.tsx` nav labels/headings/footer now use the same `lg:hidden`-scoped-class pattern the brand title already used, instead of an unscoped `{!collapsed && ...}` JS conditional.
- [ ] None of the fixes above were live-verified on a real device this session (no browser automation available) — worth a real-device pass, including a tablet per the user's "might be the same for tablets" note. **None of them have been deployed to production yet either** — all are uncommitted local changes as of 2026-10-07.

## Cross-cutting (appear more than once above)
- **No notification/delivery layer beyond outbound invoice email** — job reminders and appointment reminders both have zero SMS/email/push mechanism.
- **Observability (monitoring/alerting/error-tracking) is still fully absent**, system-wide — CI build/test gate is now in place (2026-10-11), but no deploy-automation or runtime-observability tooling.
- **Several design/todo docs under `docs/` are stale** in both directions (describe less than what shipped, or more) — always cross-check the matching `docs/features/*.md` file before trusting them.
