# Appointments & Google Calendar Sync

**Last verified:** 2026-10-07 (read against `Controllers/{Appointments,CalendarSync,PublicBooking}Controller.cs`, `Services/{Appointment,GoogleCalendarClient,CalendarSyncJob,CalendarEventMapper}.cs`, `Entities/Appointment.cs`, `mobmek_frontend/src/pages/{Appointments,CalendarSyncSettings}Page.tsx`, `components/appointments/*`, `docs/google-calendar-sync-{design,todo}.md`)

Memory previously summarized this as "all of v1 shipped, live-verified." Re-verified against current code — the *mechanism* described in the design docs is accurate, but some informal framing (notably "convert-on-arrival") doesn't match what the code actually does.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Soft-contact appointment creation | Working | `Services/AppointmentService.cs:256-277`, `AppointmentForm.tsx` |
| Convert-on-arrival | Working, but manual 3-step wizard, not automatic | `AppointmentDetailModal.tsx:106-131` |
| Google OAuth connect flow | **Does not exist — by design** | `Services/GoogleCalendarClient.cs` (uses a service account, not OAuth) |
| Event push to Google (app → Calendar) | Working | `Services/CalendarSyncJob.cs` |
| Event pull from Google (Calendar → app) | **Does not exist — one-way by design** | — |
| Conflict/dedup handling | Partial — overwrite-on-drift, not real conflict resolution | `CalendarSyncJob.cs:303-308` |
| Appointment reminders | **Missing** | — |

## Details

### Soft-contact creation — Working
`Appointment` carries both hard links (`CustomerId`/`CarId`/`JobId`/`MechanicId`, all nullable) and soft-contact snapshot fields (`ContactName`/`ContactPhone`/`ContactEmail`/`VehicleDescription`). `AppointmentService.ValidateAsync` enforces "linked customer OR (name + phone)". There's also an anonymous public-booking path (`PublicBookingController.cs`, produces `AppointmentStatus.Requested`) — functionally part of this capability but not covered by the original design doc. Well tested (`AppointmentServiceTests.cs`, 453 lines).

### Convert-on-arrival — Working, but it's a manual wizard
**Correction vs. the "soft-contact → convert-on-arrival" framing used elsewhere:** there is no backend logic that triggers conversion when status becomes `Arrived`. It's a 3-step manual UI wizard in `AppointmentDetailModal.tsx` (create customer → add car → create job), available any time the appointment lacks a job, regardless of status. `Arrived` is set as a **side effect of finishing step 3** (`NewJobPage.tsx:251-257`), not a cause of it. Each step is a separate API call orchestrated client-side — a partial failure can leave an appointment linked to a customer but no car/job.

### Google Calendar auth — service account, not OAuth
`GoogleCalendarClient.cs:170-190` loads a **service-account JSON key** (`GoogleCalendar:CredentialsJson` env var, or a file path). No OAuth2 authorization-code flow, no refresh token, no DB-stored credential table exists anywhere. `CalendarSyncSettingsPage.tsx` is explicitly read-only diagnostics (configured/not-configured badge + "Sync now" button) — there's no "Connect to Google" action anywhere because none is needed for this design. Matches the design doc's documented deviation (env var, not file-mount).

### Event push — Working
Durable outbox pattern: `CalendarSyncItem` entity (`Upsert`/`Delete`), enqueued from `AppointmentService` create/update/delete (gated on `IsConfigured`, coalesced so repeated edits don't spam the queue). `CalendarSyncJob` is a `BackgroundService`, 30s poll interval, batch 20, insert-vs-update decided by checking if the event actually exists (handles legacy foreign event IDs from an old personal calendar), exponential backoff (1m→5m→30m→1h), error-level log after 24h of failures.

### Event pull — does not exist, by design
`ReconcileCoreAsync` lists upcoming Google events only to detect drift (delete orphans, re-enqueue a push when an appointment's event is missing/stale) — it **never** creates or updates an `Appointment` row from Google data. No webhook/push-notification channel registration exists. This matches the design doc's explicit "two-way sync — rejected" decision; not a gap, a deliberate scope boundary. `CalendarSyncSettingsPage.tsx` states this in the UI copy.

### Conflict/dedup — Partial
"Conflict handling" is really one-directional overwrite: any edit made directly in Google Calendar is detected as drift and silently overwritten from Postgres on the next hourly reconcile (or on-demand). Correct per the "app is source of truth" design, but there's no actual conflict surfacing — "resolution" means always discarding the Google-side change. No overlap/double-booking check exists for appointments themselves (two appointments for the same mechanic/time slot can both be created — `ValidateAsync` has no such check).

### Appointment reminders — Missing
No code anywhere sends an SMS/email reminder tied to an upcoming appointment. (Note: `ReminderTemplate`/`Reminder` entities are an unrelated general vehicle-maintenance-reminder feature — see `jobs-workshop-floor.md` — not appointment reminders.) The design doc explicitly deferred a future `AppointmentConfirmation` email template to a later phase; it doesn't exist yet. Google-side calendar reminders (the account's own default notification settings) are the only reminder mechanism in effect today, and the app has no control over them.

## Drift vs. design docs
| Doc claim | Reality |
|---|---|
| Service-account auth, one-way push, outbox+backoff, reconcile/backfill, color mapping, legacy-id dedup | Matches code exactly — no drift |
| `CredentialsPath` (file mount) described as primary | Code/compose actually use `CredentialsJson` env var as the real deployed mechanism — already documented as a deliberate deviation in the todo doc |
| Doesn't mention `PublicBookingController`/`AppointmentStatus.Requested` | Exists in code, undocumented in these docs — code is ahead of docs here |
| "Convert-on-arrival" framing (used informally, incl. in memory) implies an automatic transition | Actual implementation is a manual 3-step wizard; `Arrived` is a byproduct, not a trigger — informal framing is imprecise |

## Recommended follow-ups
- If double-booking the same mechanic/slot is a real-world problem, add an overlap check to `AppointmentService.ValidateAsync`.
- Decide whether appointment reminders (SMS/email ahead of the appointment) are worth building — currently zero mechanism exists.
- Consider making the 3-step convert-on-arrival wizard transactional, or at least resumable, so a mid-wizard failure doesn't leave an appointment half-linked.
