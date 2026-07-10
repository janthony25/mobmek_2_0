# Google Calendar Sync — Implementation Checklist (v1)

Tracks delivery of [`google-calendar-sync-design.md`](./google-calendar-sync-design.md). Section references (§) point into the design doc.

**Status:** Not started · plan written 2026-07-11. Phases ship in order; each leaves the app fully working (sync stays cleanly disabled until Phase 0's config lands, §2). The `Google.Apis.Calendar.v3` dependency needs sign-off first (§1).

---

## Phase 0 — Google-side setup & config plumbing (owner + operator, §7)

- [ ] Google Cloud: reuse the legacy project if the owner still has access (Calendar API + service account already exist — create a fresh JSON key); otherwise new project → enable Calendar API → create service account → download JSON key
- [ ] Owner creates the **"Mobmek Workshop"** calendar under their own Google account (§7 step 2)
- [ ] Owner shares it with the service account's email — permission **"Make changes to events"** — and notes the calendar id (`…@group.calendar.google.com`) from the calendar's settings page
- [ ] Owner shares it with each staff Gmail ("See all event details"); iPhone + Apple Calendar users do the one-time `calendar.google.com/calendar/syncselect` tick (§1)
- [ ] Store secrets: key file path + calendar id via `dotnet user-secrets` locally (`GoogleCalendar:CredentialsPath` or `GoogleCalendar:CredentialsJson`, `GoogleCalendar:CalendarId`); `.env.example` gains commented `GOOGLE_CALENDAR_CREDENTIALS_PATH` / `GOOGLE_CALENDAR_ID`; compose maps them into the api service and mounts the key file read-only
- [ ] **Expected output:** a hand-run test (one-off script or `sqlcmd`-style curl against the Calendar API) inserts an event on the Workshop calendar as the service account, and it appears in the owner's phone app within seconds

---

## Phase 1 — Sync core: outbox + push job (§3, §4, §5)

- [ ] Add `Google.Apis.Calendar.v3` to `MobmekApi.csproj` *(dependency sign-off)*
- [ ] `CalendarSyncItem` entity + `CalendarSyncAction` enum + `AppDbContext` config: no FK to Appointment, unique partial index on `AppointmentId` where `Action = 'Upsert'`, index on `NextAttemptUtc` (§3.1) — migration `AddCalendarSyncItem`
- [ ] `IGoogleCalendarClient` / `GoogleCalendarClient`: lazy singleton wrapper over `CalendarService` (legacy `TryInitialize` pattern — missing/unreadable config → disabled + one startup log line, §2); `InsertAsync` / `UpdateAsync` / `DeleteAsync` / `ListUpcomingAsync`, 404/410 on delete surfaced as success (§4)
- [ ] `CalendarEventMapper`: summary prefix (customer/contact name — title), UTC + `Pacific/Auckland`, description block with linked-record fallbacks (Customer→ContactName, Car→VehicleDescription), mechanic line, `{Frontend:BaseUrl}/appointments` link, six-status `ColorId` table (§5)
- [ ] Enqueue hooks in `AppointmentService`: create/update → `Upsert` row in the same transaction; delete → snapshot `GoogleEventId` into a `Delete` row + remove any pending `Upsert`; no-op when unconfigured (§4)
- [ ] `CalendarSyncJob` (hosted, pattern: `OutboundStatusPollJob`): every 30 s take due rows oldest-first; Upsert = insert-vs-update per §4 (insert when id null **or not on the Workshop calendar** — covers the 230 legacy ids); success deletes the row + stores `GoogleEventId`; failure records `LastError` + backoff 1 m → 5 m → 30 m → hourly, error-level log past 24 h
- [ ] Service tests with `FakeGoogleCalendarClient` per §8: enqueue atomicity + coalescing + delete-snapshot, insert/update selection incl. legacy-foreign-id, backoff schedule, vanished-appointment drop, mapper content (all six colors, fallback chains)
- [ ] **Expected output:** booking an appointment in the UI puts it on the owner's phone within ~30 s; editing moves it; cancelling turns it red; hard delete removes it; stopping Google (revoke share) queues rows that flush when restored — all with zero change to booking latency

---

## Phase 2 — Backfill, reconcile & status endpoint (§6)

- [ ] Backfill: enqueue `Upsert` for every future (`StartUtc >= now`), non-cancelled appointment — exposed as `POST api/calendarsync/reconcile?backfill=true` (admin-only)
- [ ] Reconcile pass in `CalendarSyncJob` (hourly + on-demand via `POST api/calendarsync/reconcile`): list Workshop-calendar events from today forward → delete orphans, re-enqueue missing, re-enqueue drifted (app state always wins, §6)
- [ ] `GET api/calendarsync/status` (admin): configured flag, outbox depth, oldest pending, last reconcile, last error
- [ ] Reconcile/backfill tests per §8 (orphan/missing/drift/past-ignored; backfill selection)
- [ ] Runbook note in the design doc: duplicate future events on the owner's *old personal* calendar (legacy twins) are a one-time hand-cleanup — untick or delete (§6)
- [ ] **Expected output:** first enable = configure (Phase 0) + one `reconcile?backfill=true` call → every future appointment on the phone; hand-created event on the Workshop calendar disappears within the hour; GCal-side edit reverts within the hour

---

## Phase 3 — UI touches (optional, small)

- [ ] "On Google Calendar ✓ / syncing… / sync failed" indicator on the appointment detail (drive from `GoogleEventId` + presence of a pending/failed outbox row)
- [ ] Settings page card (read-only): configured status + outbox stats from `GET api/calendarsync/status`, "Sync now" button → reconcile endpoint
- [ ] **Expected output:** the owner can answer "why isn't this on my phone?" without asking a developer

---

## Deferred (not in v1, §9)

- [ ] Two-way sync (rejected §1 — revisit only with strong evidence)
- [ ] Customer invites / attendees (belongs to the email module's `AppointmentConfirmation`)
- [ ] Per-mechanic calendars routed by `MechanicId`
- [ ] DB-backed settings for calendar id / credentials rotation
