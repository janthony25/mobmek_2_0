# Jobs / Workshop Floor

**Last verified:** 2026-10-12 (read against `Controllers/{Jobs,JobItems,JobServices,JobServiceLines,JobPhotos,JobFiles,Labour,Products,Notes,Reminders,ReminderTemplates}Controller.cs`, matching `Services/`/`Entities/`, `mobmek_frontend/src/pages/{JobCenter,JobDetail,NewJob,JobServices,Products,NotesReminders}Page.tsx`, test suite — 666/666 passing)

Covers the core job-card workflow: status, items/parts, labour, job photos (DVI candidate), job→invoice handoff, and the reminders/notes/reminder-templates side features. This is the area with the most real gaps vs. what a shop-management tool typically needs.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Job status lifecycle | **Partial — no state machine** | `Services/JobService.cs:150-180` |
| Parts / products linkage | **Broken as a concept** | `Entities/JobItem.cs`, `Entities/Product.cs` |
| Labour time tracking | Partial — manual hours only | `Entities/Labour.cs` |
| Job photos / DVI | **Partial — bare upload only** | `Controllers/JobPhotosController.cs`, `components/jobs/JobPhotos.tsx` |
| Job files (any type, S3-backed) | Working (new 2026-10-12) | `Controllers/JobFilesController.cs`, `components/jobs/JobFiles.tsx` |
| Catalog services on a job (+ surcharge) | Working (surcharge added 2026-10-12) | `Entities/JobServiceLine.cs`, `components/jobs/ServicesEditor.tsx` |
| Reminders (delivery) | **Missing — internal to-do list only** | `Entities/Reminder.cs`, `Services/ReminderService.cs` |
| Reminder Templates | Working | `Controllers/ReminderTemplatesController.cs` |
| Notes | Working | `Controllers/NotesController.cs` |
| Job → Invoice linkage | Working, but status-independent | `Services/InvoiceService.cs` |

## Details

### Job status lifecycle — Partial, no enforcement
`JobStatus` enum: `Open, InProgress, AwaitingParts, Completed, Invoiced`. `JobService.UpdateAsync` (`Services/JobService.cs:150-180`) sets `job.Status` directly with **zero transition validation** — any status can move to any other at any time (e.g. `Invoiced → Open` is accepted). Frontend `JobDetailPage.tsx:388-396` is a free `<select>` with no guard rails either. Totals (`TotalJobPrice`/`TotalJobProfit`) are correctly server-recomputed on every mutation. Margin redaction for non-privileged roles (`JobRoleRedaction.cs`) is solid.

### Parts / products linkage — effectively broken, two disconnected systems
`JobItem` parts are **free-text** (`ItemName` string) — no `ProductId` FK anywhere (confirmed via grep across backend and frontend). `Product` entity's own doc comment says it's *"a sample domain entity demonstrating the EF Core + service + controller flow — replace or extend with the real Mobmek domain model."* `ProductsPage.tsx` is a generic CRUD page that job-item entry never references. **There is no real inventory/parts catalog backing jobs; stock quantities on `Product` are decorative — nothing decrements them.**

### Labour — Partial, manual entry not a timer
`Labour` entity is `Hours` + `RatePerHour` + optional `FixedAmount` override — a manual numeric line item, not clock-in/clock-out tracking. No start/stop timestamps, no mechanic punch clock.

### Job photos / DVI — Partial, confirms the industry-gap finding
`JobPhoto` entity only has `FileName`/`ContentType`/`StorageKey`/`SizeBytes` — no category/tag, no inspection checklist, no pass/fail/severity, no annotation, no linkage to a specific job item, no customer-facing report. `JobPhotosController.cs` logic is just a 25MB cap + image-content-type check. Frontend (`components/jobs/JobPhotos.tsx`) is upload (file picker or live camera) → thumbnail grid → remove, nothing more. **This is literally a bare photo-upload endpoint — there is no DVI feature.**

**Bug fixed 2026-10-07:** the thumbnail grid's "Remove" button called `deleteJobPhoto` straight from `onClick` with no confirmation — a mis-click permanently deleted a photo instantly, reported by the user. `JobPhotosSection` in `JobPhotos.tsx` now routes removal through the shared `ConfirmDialog` (sets a pending `deleting` photo, deletes only `onConfirm`), matching the pattern used elsewhere in the app (`CrudSection`, `CarDetailPage`, etc.) — now also written up as a standing rule in `mobmek_frontend/CLAUDE.md` ("any irreversible delete goes through `ConfirmDialog`"). An audit of every other direct `delete*`/`remove*` call in the frontend found two more instances of the same bug (business logo removal, cash-transaction attachment removal — see `business-settings.md` and `cash-flow-gst.md`) and confirmed everything else already confirms properly. Not live-verified on a real device this session (no browser automation available) — type-checked and linted clean.

Storage: `IFileStorage` abstraction with `LocalFileStorage`/`S3FileStorage` implementations, selected via `FileStorage__Provider` env var (default `Local`). **Correction (2026-10-07): not actually a gap.** `infrastructure/README.md`'s deploy checklist confirms production is provisioned with `FileStorage:Provider=S3` against a real, versioned, encrypted bucket (`mobmek-uploads-649058763120`) — specific enough (bucket name, region, lifecycle policy, IAM grants) to trust as done, not aspirational. S3's own versioning covers accidental-delete recovery for uploads; `scripts/backup-to-s3.sh` only backing up Postgres is correct, not an oversight — uploads don't need a separate backup script when the object store itself is durable and versioned.

### Job files (any type) — Working (added 2026-10-12)
A new `JobFile` entity (`Entities/JobFile.cs`) mirrors `JobPhoto` exactly — `FileName`/`ContentType`/`StorageKey`/`SizeBytes`, deleted-with-job storage cleanup in `JobService.DeleteAsync` — but accepts **any** content type (25 MB cap, same as photos), unlike `JobPhotosController`'s `image/*`-only check. Goes through the same `IFileStorage` abstraction, so it lands in S3 in production exactly like photos do. `JobFilesController.cs`: `GET/POST /api/jobs/{jobId}/files`, `GET/DELETE /api/jobs/{jobId}/files/{id}`. Frontend: new "Files" section on `JobDetailPage.tsx` (below Photos) via `components/jobs/JobFiles.tsx` (`JobFilesSection`), plus a draft picker (`JobFileDraftPicker`) on `NewJobPage.tsx` that uploads once the job is created, mirroring the existing photo-draft flow. Delete is gated by `ConfirmDialog`, per the standing rule. 6 new backend tests (`JobFileServiceTests.cs`).

### Catalog services on a job — Working, now supports a per-line surcharge (added 2026-10-12)
`JobServiceLine.AdditionalAmount` (decimal, default 0) is a one-off surcharge added on top of the catalog's snapshotted `UnitPrice`, for services whose listed price is a baseline that can run higher on a given job (e.g. "General Service: $200" catalog price, billed at $250 on a specific job). `LineTotal = UnitPrice × Quantity + AdditionalAmount` — not multiplied by quantity. The invoice/quotation line shows one consolidated price (`ItemPrice = UnitPrice + AdditionalAmount`), never a separate "+ $50" breakdown — verified via `InvoiceServiceTests.GenerateAsync_FoldsServiceLineAdditionalAmount_IntoOneConsolidatedPrice`. Frontend: ticking a service in `components/jobs/ServicesEditor.tsx` (shared by `NewJobPage`/`JobDetailPage` edit mode) reveals a "+ Add amount" control that opens a small popup; the service's displayed price updates live to catalog + surcharge. 2 new backend tests (`JobServiceLineServiceTests.cs`) plus the invoice-mapping test above.

### Reminders — Missing delivery mechanism
`Reminder` entity has no `SentAt`, no channel field, no notification log. `RemindersController`/`ReminderService` are pure CRUD — no send action anywhere. None of the app's 4 registered background services (`RecurringTransactionPostingJob`, `OutboundStatusPollJob`, `AccountPurgeJob`, `CalendarSyncJob`) reference `IReminderService`. Frontend only highlights overdue reminders in red; "mark done" is manual. **Reminders are purely an internal staff to-do list — zero customer-facing SMS/email/push delivery**, confirming the "SMS" gap flagged in the industry-readiness analysis.

### Reminder Templates — Working
Simple CRUD presets feeding the reminder form, write-gated by `ManageReminderTemplates`.

### Notes — Working
Freeform sticky-note board (optional due date, color, pinned, done), optionally scoped to a customer. Deliberately not job-scoped — dated items are Reminders.

**`Job.JobNotes`/`Job.InvoiceNotes` (2026-10-13):** these two plain-text fields stay purely free-editable via the job form — the invoice partial-payments feature (`invoices-quotes-public-booking.md`) does **not** write into them (an early version did, briefly, then was reverted the same day because it cluttered these fields and leaked onto the customer-facing invoice PDF via `Invoice.Notes`). `JobForm.tsx` instead renders a separate read-only `PartialPaymentHistoryList` directly above the Invoice Notes field, aggregated from the job's invoices' `partialPayments`, with its own per-row edit/delete controls.

### Job → Invoice — Working, but decoupled from status
`InvoiceService` snapshots `job.Items`/`job.Labour`/`job.ServiceLines` into a new Invoice/Quotation at creation time. **No gate on `Job.Status`** — an invoice can be generated regardless of whether the job is Open, InProgress, or Completed, and `JobStatus.Invoiced` is never auto-set by invoice creation; it's purely an operator-chosen, unenforced value. Quotation acceptance correctly re-snapshots from the quotation (not the live job) and only accepts `Status == "Active"` quotations.

### Job detail back-link — Working (bug fixed 2026-10-12)
`JobDetailPage.tsx` reads `location.state.from`/`fromLabel` (set by the linking page) to render its "← Back to X" link, defaulting to `/jobs` ("Job Center") when no state is passed — by design, so a job reached from a bookmark/direct link still has somewhere sane to go. `CustomerDetailPage.tsx` and `components/invoices/DocumentListPage.tsx` already passed this `state`. **Bug:** `CarDetailPage.tsx`'s own job list (`AppointmentRow`, line ~162) linked to `/jobs/${job.id}` with no `state` at all, so a job opened from Customer → Car → that car's jobs list always fell through to the "Job Center" default instead of back to the car page, discarding the customer/car context the user had drilled into. Fixed by building the same `backState` shape (`{ from: '/customers/{customerId}/cars/{carId}', fromLabel: '<year> <make> <model>' }`) `CarDetailPage` already had everything to construct, and passing it to the job `Link`.

### Job edit-page UX (2026-10-12)
Several small usability fixes to `NewJobPage.tsx`/`JobDetailPage.tsx` edit mode: item-name inputs in `PartsEditor` now uppercase as-typed (matching the Title field); item-card borders darkened (`slate-200` → `slate-300`) across `PartsEditor`/`LabourEditor`/`DiscountEditor`/the services grid for clearer row separation; "+ Add part" now prepends the new row to the top of the Parts & Items list instead of appending to the bottom; "Total profit" is now shown beside "Estimated total" in the fixed bottom bar on both pages, computed client-side with the same formula as the backend's `Job.TotalJobProfit` (item profit + labour + services − discount, since neither labour nor services track a cost). The Invoices and Quotations sections on `JobDetailPage.tsx` (list + Generate button + all row actions) are no longer rendered at all while the job is in edit mode — they're unnecessary while mid-edit and reappear on Save/Cancel back to view mode.

### Tests
666/666 passing across `JobServiceTests`, `JobItemServiceTests`, `JobPhotoServiceTests`, `JobFileServiceTests`, `JobServiceCatalogServiceTests`, `JobServiceLineServiceTests`, `LabourServiceTests`, `NoteServiceTests`, `ReminderServiceTests`, `ReminderTemplateServiceTests`, `ProductServiceTests`, `JobRoleRedactionTests`, plus the full rest of the suite. Coverage is service-layer only (repo convention) — no test exercises status transitions (none exist to test) or reminder delivery (none exists). Frontend changes above are type-checked/linted clean (`tsc -b`, `oxlint`) but not live-browser-verified this session — the automation browser's network layer returned stale, pre-rebuild JS/CSS bundles even for brand-new tabs and cache-bypassed fetches, a quirk isolated to that tooling (confirmed via direct `curl` against the rebuilt container, which always returned the correct, current bundle).

## Confirmed gaps (these were hypotheses going in — all confirmed real)
1. No real DVI/photo workflow — bare upload only.
2. No reminder delivery mechanism — internal to-do list only.
3. No parts/inventory linkage — `Product` is unused scaffolding; job parts are untracked free text.
4. *(new finding)* No job-status state machine; invoice generation is status-independent.

## Recommended follow-ups
- Decide whether `Product`/`ProductsController` is worth building out into a real inventory system, or should be removed to stop it looking like a feature.
- If DVI matters for the business, that's a from-scratch build (checklist model, categories, customer-facing report), not a tweak to `JobPhoto`.
- Add explicit job-status transition rules if operators are mis-clicking statuses in practice.
- ~~Decide whether job photos need backup coverage in production~~ — not a gap, confirmed 2026-10-07 (production uses versioned S3, not local disk).
