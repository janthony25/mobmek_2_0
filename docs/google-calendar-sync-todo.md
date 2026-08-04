# Google Calendar Sync — Implementation Checklist (v1)

Tracks delivery of [`google-calendar-sync-design.md`](./google-calendar-sync-design.md). Section references (§) point into the design doc.

**Status:** Phases 0–3 shipped 2026-08-03 (live-tested end to end, including in-browser: badge renders on a real synced appointment, settings page + "Sync now" button work). Phases ship in order; each leaves the app fully working (sync stays cleanly disabled until Phase 0's config lands, §2). The `Google.Apis.Calendar.v3` dependency sign-off is done (§1).

---

## Phase 0 — Google-side setup & config plumbing (owner + operator, §7)

- [x] Google Cloud: owner created a **brand-new, standalone project** (`mobmek-v2`) rather than reusing the legacy one — deliberate choice for zero shared surface with the still-live legacy system (verified safe either way: isolation comes from the calendar id in config, not the GCP project) → enabled Calendar API → created service account `mobmekv2-calendar-system@mobmek-v2.iam.gserviceaccount.com` → downloaded JSON key
- [x] Owner created the **"Mobmek Workshop"** calendar under their own Google account (§7 step 2)
- [x] Owner shared it with the service account's email — permission **"Make changes and see all event details"** — and noted the calendar id from the calendar's settings page
- [ ] Owner shares it with each staff Gmail ("See all event details"); iPhone + Apple Calendar users do the one-time `calendar.google.com/calendar/syncselect` tick (§1) — deferred, single-mechanic shop for now
- [x] Store secrets: **deviated from the doc's file-mount wording** — used `GoogleCalendar:CredentialsJson` (raw JSON, minified) + `GoogleCalendar:CalendarId` as plain env vars via `.env`/docker-compose (same secret-string pattern as `Email:Resend:ApiKey`, no volume mount needed); `.env.example` has commented `GOOGLE_CALENDAR_CREDENTIALS_JSON` / `GOOGLE_CALENDAR_ID`
- [x] **Expected output:** verified live — see Phase 1's expected-output line below, confirmed via direct API calls (create/edit/cancel/delete)

---

## Phase 1 — Sync core: outbox + push job (§3, §4, §5)

- [x] Add `Google.Apis.Calendar.v3` to `MobmekApi.csproj` *(dependency sign-off)* — uses the non-obsolete `CredentialFactory.FromJson<ServiceAccountCredential>(...).ToGoogleCredential()` path rather than `GoogleCredential.FromJson`/`FromStream` (both obsolete in 1.75.0)
- [x] `CalendarSyncItem` entity + `CalendarSyncAction` enum + `AppDbContext` config: no FK to Appointment, unique partial index on `AppointmentId` where `Action = 'Upsert'`, index on `NextAttemptUtc` (§3.1) — migration `AddCalendarSyncItem`
- [x] `IGoogleCalendarClient` / `GoogleCalendarClient`: lazy singleton wrapper over `CalendarService` (legacy `TryInitialize` pattern — missing/unreadable config → disabled + one startup log line, §2); `InsertAsync` / `UpdateAsync` / `DeleteAsync` / `ListUpcomingAsync`, 404/410 on delete surfaced as success (§4)
- [x] `CalendarEventMapper`: summary prefix (customer/contact name — title), UTC + `Pacific/Auckland`, description block with linked-record fallbacks (Customer→ContactName, Car→VehicleDescription), mechanic line, `{Frontend:BaseUrl}/appointments` link, six-status `ColorId` table (§5)
- [x] Enqueue hooks in `AppointmentService`: create/update → `Upsert` row in the same transaction; delete → snapshot `GoogleEventId` into a `Delete` row + remove any pending `Upsert`; no-op when unconfigured (§4)
- [x] `CalendarSyncJob` (hosted, pattern: `OutboundStatusPollJob`): every 30 s take due rows oldest-first; Upsert = insert-vs-update per §4 (insert when id null **or not on the Workshop calendar** — covers the 230 legacy ids); success deletes the row + stores `GoogleEventId`; failure records `LastError` + backoff 1 m → 5 m → 30 m → hourly, error-level log past 24 h
- [x] Service tests with `FakeGoogleCalendarClient` per §8: enqueue atomicity + coalescing + delete-snapshot, insert/update selection incl. legacy-foreign-id, backoff schedule, vanished-appointment drop, mapper content (all six colors, fallback chains) — 29 tests
- [x] **Expected output:** verified live against the real Workshop calendar via direct API calls — booked appointment pushed within 30s (`googleEventId` stored), edit-to-Cancelled pushed the color change, hard delete removed the event; zero real errors in logs throughout

---

## Phase 2 — Backfill, reconcile & status endpoint (§6)

- [x] Backfill: enqueue `Upsert` for every future (`StartUtc >= now`), non-cancelled appointment — exposed as `POST api/calendarsync/reconcile?backfill=true` (admin-only). Refinement beyond the doc's literal wording: the ongoing reconcile "missing" check *also* skips appointments that are `Cancelled` **and** have never been synced (`GoogleEventId is null`) — otherwise reconcile (which runs immediately after backfill in the same call) would instantly re-enqueue exactly what backfill deliberately skipped. An appointment that *was* synced and then vanished from the calendar still gets recreated regardless of status — the app remains the source of truth for anything it once pushed.
- [x] Reconcile pass in `CalendarSyncJob` (hourly + on-demand via `POST api/calendarsync/reconcile`): list Workshop-calendar events from today forward → delete orphans, re-enqueue missing, re-enqueue drifted (app state always wins, §6)
- [x] `GET api/calendarsync/status` (admin): configured flag, outbox depth, oldest pending (by `CreatedAtUtc`), last reconcile, last error — backed by a new ephemeral (not persisted, resets on restart) `CalendarSyncStatus` singleton
- [x] Reconcile/backfill tests per §8 (orphan/missing/drift/past-ignored; backfill selection) — 8 tests
- [ ] Runbook note in the design doc: duplicate future events on the owner's *old personal* calendar (legacy twins) are a one-time hand-cleanup — untick or delete (§6) — not yet written up
- [x] **Expected output:** verified live — `GET/POST api/calendarsync/status` and `POST api/calendarsync/reconcile?backfill=true` both exercised against the running stack (empty system at the time, so zero outbox depth / no orphans found, but the full request→response→status-update path is confirmed working with no errors)

---

## Phase 3 — UI touches (optional, small)

- [x] "On Google Calendar" indicator on the appointment detail — **simplified from the doc's three-state wording**: shows a "📆 On Google Calendar" badge (with the event id as a tooltip) purely from `GoogleEventId` presence; no separate "syncing…/failed" states since that needs per-appointment outbox visibility, which no endpoint exposes yet (the aggregate `/status` endpoint only gives a whole-system outbox depth, not "is *this* appointment's row pending/failed") — judged not worth a new endpoint for a "small" UI touch. Absence of the badge shows nothing (no "not synced" text), which stays correct whether sync is just disabled or the appointment simply predates its first push.
- [x] Settings page card (read-only) at `/calendar-sync` (Settings nav group, admin-only): configured status + outbox depth/oldest-pending/last-reconcile/last-error from `GET api/calendarsync/status`, "Sync now" button → `POST api/calendarsync/reconcile` (no backfill flag — that's a one-time go-live action, left to a direct API call)
- [x] **Expected output:** verified live in-browser — created a real appointment via API, waited for it to sync, opened it in the UI and confirmed the badge renders; loaded the Calendar Sync settings page and confirmed status fields populate and "Sync now" updates them with a success toast

---

## Deferred (not in v1, §9)

- [ ] Two-way sync (rejected §1 — revisit only with strong evidence)
- [ ] Customer invites / attendees (belongs to the email module's `AppointmentConfirmation`)
- [ ] Per-mechanic calendars routed by `MechanicId`
- [ ] DB-backed settings for calendar id / credentials rotation
