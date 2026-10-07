# Jobs / Workshop Floor

**Last verified:** 2026-10-07 (read against `Controllers/{Jobs,JobItems,JobServices,JobServiceLines,JobPhotos,Labour,Products,Notes,Reminders,ReminderTemplates}Controller.cs`, matching `Services/`/`Entities/`, `mobmek_frontend/src/pages/{JobCenter,JobDetail,NewJob,JobServices,Products,NotesReminders}Page.tsx`, test suite — 144/144 passing)

Covers the core job-card workflow: status, items/parts, labour, job photos (DVI candidate), job→invoice handoff, and the reminders/notes/reminder-templates side features. This is the area with the most real gaps vs. what a shop-management tool typically needs.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Job status lifecycle | **Partial — no state machine** | `Services/JobService.cs:150-180` |
| Parts / products linkage | **Broken as a concept** | `Entities/JobItem.cs`, `Entities/Product.cs` |
| Labour time tracking | Partial — manual hours only | `Entities/Labour.cs` |
| Job photos / DVI | **Partial — bare upload only** | `Controllers/JobPhotosController.cs`, `components/jobs/JobPhotos.tsx` |
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

### Reminders — Missing delivery mechanism
`Reminder` entity has no `SentAt`, no channel field, no notification log. `RemindersController`/`ReminderService` are pure CRUD — no send action anywhere. None of the app's 4 registered background services (`RecurringTransactionPostingJob`, `OutboundStatusPollJob`, `AccountPurgeJob`, `CalendarSyncJob`) reference `IReminderService`. Frontend only highlights overdue reminders in red; "mark done" is manual. **Reminders are purely an internal staff to-do list — zero customer-facing SMS/email/push delivery**, confirming the "SMS" gap flagged in the industry-readiness analysis.

### Reminder Templates — Working
Simple CRUD presets feeding the reminder form, write-gated by `ManageReminderTemplates`.

### Notes — Working
Freeform sticky-note board (optional due date, color, pinned, done), optionally scoped to a customer. Deliberately not job-scoped — dated items are Reminders.

### Job → Invoice — Working, but decoupled from status
`InvoiceService` snapshots `job.Items`/`job.Labour`/`job.ServiceLines` into a new Invoice/Quotation at creation time. **No gate on `Job.Status`** — an invoice can be generated regardless of whether the job is Open, InProgress, or Completed, and `JobStatus.Invoiced` is never auto-set by invoice creation; it's purely an operator-chosen, unenforced value. Quotation acceptance correctly re-snapshots from the quotation (not the live job) and only accepts `Status == "Active"` quotations.

### Tests
144/144 passing across `JobServiceTests`, `JobItemServiceTests`, `JobPhotoServiceTests`, `JobServiceCatalogServiceTests`, `JobServiceLineServiceTests`, `LabourServiceTests`, `NoteServiceTests`, `ReminderServiceTests`, `ReminderTemplateServiceTests`, `ProductServiceTests`, `JobRoleRedactionTests`. Coverage is service-layer only (repo convention) — no test exercises status transitions (none exist to test) or reminder delivery (none exists).

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
