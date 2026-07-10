# Google Calendar Sync — Design Document (v1)

**Status:** Planned · **Date:** 2026-07-11
**Scope:** One-way mirror of Mobmek appointments onto a dedicated Google Calendar, so the schedule is visible on any phone (with Google's own notifications) without opening the app.

**Why:** for a mobile mechanic the phone calendar is the real day-to-day view — on the road, at a glance, with reminders. The legacy system already proved this works for the business (`MobileMekaniko_Final/Services/GoogleCalendarService.cs` pushed events for years; 230 imported appointments carry `GoogleEventId`s from it). This module keeps that value while fixing what legacy cut corners on: events pushed inline with no retry (a failed call = event silently missing forever), events written into the owner's *personal* calendar, and no drift correction.

---

## 1. Decisions (settled 2026-07-11)

| Concern | Decision | Why |
|---|---|---|
| **Direction** | **One-way push** — Postgres is the source of truth; Google Calendar is a read-only mirror. "Book and edit in the app, view anywhere." | Two-way sync means watch channels, sync tokens, conflict resolution, and GCal edits that lack the structured links (customer/car/job) our appointments need. ~90% of the value at ~10% of the cost. Same direction legacy used. |
| **Auth** | **Service account** (JSON key file), same mechanism as legacy. Reuse the existing Google Cloud project/service account if the owner still has access; otherwise create fresh (§7). | No OAuth consent flow, no testing-mode refresh-token expiry (7 days on consumer Gmail). A key file in config is the same secret pattern as the Resend API key. |
| **Target calendar** | A **dedicated "Mobmek Workshop" calendar** the owner creates and shares with the service account ("Make changes to events"). Calendar id in config. | Legacy wrote into the owner's personal primary calendar (`mobilemekaniko.nz@gmail.com`). A dedicated calendar can be shared with staff without exposing personal events, and because the app owns *every* event on it, wipe-and-resync and orphan cleanup are always safe (§6). |
| **Delivery** | **Durable outbox + hosted background job** with retry/backoff — never call Google inline in the request path. | Legacy called Google during the save; a failure was logged and lost (updates then no-op forever because no event id was stored). An outbox row survives restarts, retries transient failures, and keeps booking latency and availability independent of Google. |
| **Drift** | Periodic **reconcile pass** re-asserts app state onto the calendar (fix edits, recreate missing, delete orphans). | Cheap once the calendar is dedicated (§6). Legacy had none — a GCal-side edit or delete diverged permanently. |
| **Legacy event ids** | The 230 imported `GoogleEventId`s point at events legacy created in the owner's *primary* calendar — treated as historical, never adopted or updated. Backfill covers future appointments only (§6). | Old events are a past-schedule record on a calendar we deliberately no longer write to. |
| **Phone visibility** | Plain Google Calendar sharing — the owner shares the Workshop calendar with each person's Gmail address; it appears in their Google Calendar app live. | No app involvement; Google handles device sync, offline, notifications. iPhone + Apple Calendar users need the one-time `calendar.google.com/calendar/syncselect` tick (or just use the Google Calendar app). |

**New dependency requiring sign-off:** `Google.Apis.Calendar.v3` (backend) — Google's official .NET client, the same library legacy used. No frontend dependencies.

---

## 2. Design principles

1. **Sync failures are invisible to the user.** Creating, editing, or cancelling an appointment always succeeds in exactly the time Postgres takes; the mirror catches up seconds later. No Google error ever surfaces as a booking error.
2. **Unconfigured = cleanly disabled.** Missing key file or calendar id → the enqueue hooks and the job no-op with one startup log line, same graceful degradation as legacy and as the Resend key. Nothing else in the app changes.
3. **Secrets never touch the database or the repo.** The service-account JSON lives on disk/env (`dotnet user-secrets` locally, mounted file or env var in compose); config holds only its path and the calendar id.
4. **The app owns the Workshop calendar.** Every event there was created by Mobmek. That invariant is what makes reconcile trivial: any event we don't recognize is an orphan to delete, any drifted field is overwritten from Postgres. (Corollary: the owner must not hand-create events on this calendar — book in the app instead. The reconcile pass would remove them.)
5. **Follow the existing slice pattern.** Entity (`BaseEntity`) → `AppDbContext` config → service behind an interface → hosted job following `OutboundStatusPollJob` / `RecurringTransactionPostingJob` → service-level xUnit tests with a fake Google client → EF migration.
6. **Carry over what the owner already knows.** Legacy's status→color mapping (blue/yellow/green/red) and its event description layout (customer, car, contact, notes) are kept, extended for the new statuses (§5).

---

## 3. Domain model

### 3.1 `CalendarSyncItem` — durable outbox, one row per pending push

| Field | Type | Notes |
|---|---|---|
| `Action` | enum `CalendarSyncAction` | `Upsert` \| `Delete` |
| `AppointmentId?` | Guid | set for `Upsert`; **no FK** — the row must survive appointment hard-delete, and `Delete` rows have no appointment at all |
| `GoogleEventId?` | string | set for `Delete` (snapshot taken at delete time, since the appointment row is gone) |
| `Attempts` | int | incremented per failed try |
| `NextAttemptUtc` | DateTime | backoff schedule (§4); due when ≤ now |
| `LastError?` | string | most recent failure, for diagnostics |

Rows are **deleted on success** — the outbox holds only outstanding work, so "empty table" = "mirror is current". Coalescing: at most one pending `Upsert` per appointment (unique partial index on `AppointmentId` where `Action = 'Upsert'`); re-editing an appointment before its push runs just leaves the existing row (the job reads current appointment state at push time, so the latest edit always wins). A `Delete` enqueued while an `Upsert` is pending removes the pending `Upsert`.

`Appointment.GoogleEventId` (already exists, currently reserved) stores the linked event id after a successful create.

No settings entity: v1 is configured entirely from `appsettings`/env (§7). A DB-backed settings page can come later if the owner ever needs to retarget the calendar without redeploying.

---

## 4. Sync pipeline

**Enqueue (in `AppointmentService`):** after a successful create/update, and before a delete commits, write the outbox row **in the same transaction** as the appointment change — an appointment change and its sync intent are atomic. Status-only changes enqueue too (color updates, §5). When sync is unconfigured, the hooks no-op.

**Push (`CalendarSyncJob`, hosted service):** every 30 seconds, take due rows (`NextAttemptUtc <= now`, oldest first, small batch):

- `Upsert`: load the appointment fresh (skip + drop the row if it vanished). If `GoogleEventId` is null **or refers to an event not on the Workshop calendar** (covers the 230 legacy ids pointing at the old personal calendar) → `Events.Insert` on the Workshop calendar, store the new id. Otherwise → `Events.Update`.
- `Delete`: `Events.Delete` by the snapshotted id; a 404/410 ("already gone") counts as success.
- Success → delete the outbox row. Failure → record `LastError`, bump `Attempts`, schedule backoff: 1 min → 5 min → 30 min → then hourly. After 24 h of failures the row stays (still retried hourly) but the job logs at error level — a stuck outbox is a config problem (revoked share, deleted calendar), not a transient one.

The job holds one `CalendarService` client (singleton wrapper, lazy-initialized like legacy's `TryInitialize`), behind an `IGoogleCalendarClient` interface so tests fake it (§8).

---

## 5. Event mapping

| Event field | Source |
|---|---|
| `Summary` | `Title` — prefixed with the customer/contact name when present, e.g. "John Smith — Brake inspection" |
| `Start` / `End` | `StartUtc` / `EndUtc` sent as UTC instants with `TimeZone = "Pacific/Auckland"` (Google renders in the viewer's local zone regardless; the explicit zone keeps the calendar's own display correct) |
| `Description` | Line-per-fact block, legacy layout extended: customer (linked `Customer` name, else `ContactName`), phone (`Customer` phone, else `ContactPhone`), vehicle (linked `Car` make/model/rego, else `VehicleDescription`), mechanic name when assigned, `Notes`, and a final link `{Frontend:BaseUrl}/appointments` |
| `ColorId` | `Scheduled` → 9 blue · `Confirmed` → 7 peacock · `Arrived` → 5 yellow · `Completed` → 10 green · `NoShow` → 8 graphite · `Cancelled` → 11 red (legacy's four kept identical; two new statuses slotted in — owner can reskin later, it's one lookup table) |

**Cancelled keeps its event, colored red** — matching the legacy behaviour the owner is used to (a red block reads as "slot freed" at a glance and preserves the day's history on the phone). Only hard-deleting an appointment deletes the event. No attendees, no Google-side reminders in v1 — the calendar's own default notifications apply, which is what the owner had before.

---

## 6. Backfill & reconcile

**Backfill (one-time, on first enable):** enqueue an `Upsert` for every appointment with `StartUtc >= now` that isn't `Cancelled`. Past appointments are never pushed — the Workshop calendar starts at the go-live date; history lives in the app (and, incidentally, on the owner's old personal calendar from legacy). Any future-dated *imported* appointment gets a fresh event on the Workshop calendar per the §4 "not on this calendar" rule; its legacy twin on the personal calendar is left alone (if the owner sees a duplicate there, deleting the old one by hand — or unticking the old calendar — is a one-time cleanup, noted in the runbook).

**Reconcile (same hosted job, hourly):** list all events on the Workshop calendar from "today" forward and compare with Postgres:

- Event whose id matches no appointment's `GoogleEventId` → orphan (hand-created or left over) → delete.
- Appointment (future, non-deleted) whose event is missing → enqueue `Upsert`.
- Event whose summary/times/color/description differ from the appointment → enqueue `Upsert` (a GCal-side edit gets overwritten — the app is the source of truth, per §1).

Exposed as `POST api/calendarsync/reconcile` (admin) too, so "the calendar looks wrong" has a one-click fix; the same endpoint with a `backfill=true` flag runs the first-enable backfill, making go-live a single call after configuration.

`GET api/calendarsync/status` (admin) reports: configured yes/no, outbox depth, oldest pending item, last reconcile time, last error — the diagnostics page for "why isn't my phone showing it".

---

## 7. Configuration & Google-side setup

Config keys (secret-pattern identical to `Email__Resend__ApiKey`):

| Key | Meaning |
|---|---|
| `GoogleCalendar:CredentialsPath` | path to the service-account JSON key file (compose: mounted read-only volume; local: `dotnet user-secrets` may instead set `GoogleCalendar:CredentialsJson` with the raw JSON) |
| `GoogleCalendar:CalendarId` | id of the Workshop calendar (from its settings page, `…@group.calendar.google.com`) |

Both present and readable = enabled; anything missing = disabled with a startup log line. `.env.example` gains commented `GOOGLE_CALENDAR_CREDENTIALS_PATH` / `GOOGLE_CALENDAR_ID` entries; compose maps them into the api service and mounts the key file.

One-time Google-side setup (owner + operator, ~10 minutes, no code — Phase 0 of the todo):

1. Reuse the legacy Google Cloud project if the owner can still access it (the service account and enabled Calendar API are already there — just create a fresh JSON key); otherwise: new project → enable **Google Calendar API** → create service account → download JSON key.
2. Owner creates a **"Mobmek Workshop"** calendar under their own Google account.
3. Owner shares it with the service account's generated email, permission **"Make changes to events"**.
4. Owner shares it with each staff member's Gmail ("See all event details"), who then see it live in the Google Calendar app.

---

## 8. Testing

All service-level xUnit against in-memory `AppDbContext` with a `FakeGoogleCalendarClient` (records calls, scriptable failures):

- Enqueue: create/update/delete each write the right outbox row in-transaction; delete snapshots the event id and removes a pending upsert; unconfigured → no rows.
- Coalescing: double edit before push = one row; push uses latest appointment state.
- Push: insert-vs-update selection (null id, legacy foreign id, known id); success deletes the row and stores the id; 404 on delete counts as success; failure records error + backoff schedule; appointment-vanished drops the row.
- Mapping: summary prefix rules, description fallbacks (linked customer vs soft contact), all six status colors, UTC + zone.
- Reconcile: orphan deleted, missing re-enqueued, drifted field re-enqueued, past events ignored; backfill selects exactly future non-cancelled.

---

## 9. Out of scope (v1)

- **Two-way sync** (GCal edits flowing back) — rejected, §1.
- **Customer invites** (attendees on events) and email confirmations — the email module's `AppointmentConfirmation` template is the right home for that.
- **Per-mechanic calendars** (one shared calendar per `Employee`, events routed by `MechanicId`) — natural v2 once there's more than one mechanic; the outbox design already supports multiple targets.
- **Settings UI** for calendar id / credentials — config-only in v1; revisit if the owner ever needs to rotate without a redeploy.
