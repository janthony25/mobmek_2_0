# Feature Status Index

Living, code-verified status docs for every feature area in Mobmek. Purpose: answer "what do we actually have, what works, what's broken, what's still just a doc" without re-reading the whole codebase every time.

**Rules for keeping this useful:**
- Update the relevant file whenever you ship, fix, or discover something in that feature area — a stale status doc is worse than none.
- Status claims must be code-verified (file path + line/function), not inferred from a design doc, a commit message, or memory. If you can't point at the code, mark it "Unverified" rather than "Working."
- Each file's "Last verified" date should move forward only when someone actually re-checked the code, not just edited prose.
- When a design/todo doc under `docs/` drifts from reality, note the drift here rather than silently trusting the older doc.

Baseline audit performed 2026-10-07 by reading the actual backend (`mobmek_api/src/MobmekApi`) and frontend (`mobmek_frontend/src`) source, migrations, and tests — not assumptions.

**[feature-gaps.md](feature-gaps.md)** is the consolidated backlog — every gap from every file below in one checklist, for picking off work later. Keep it in sync with the per-feature files: check off / remove a gap there when you fix it, add new ones there when you find them.

## Status legend
- **Working** — verified end-to-end (backend + frontend + DB), has a real code path all the way through.
- **Partial** — real code exists but with a specific, named gap (missing enforcement, missing UI, missing delivery mechanism, etc).
- **Planned only** — a design/todo doc exists but no corresponding entity/controller/UI was found.
- **Broken** — code exists but has a demonstrable bug.
- **Missing** — flagged as a need but nothing exists at all.

## Feature areas

| Area | File | One-line state |
|---|---|---|
| Auth & Staff Management | [auth-staff-management.md](auth-staff-management.md) | Full admin-editable RBAC, account lifecycle, login audit — ahead of its own design doc |
| Customers & Vehicles | [customers-vehicles.md](customers-vehicles.md) | All known gaps closed 2026-10-07 — duplicate detection, notes panel shipped |
| Jobs / Workshop Floor | [jobs-workshop-floor.md](jobs-workshop-floor.md) | Core job/labour/invoice flow works; no status state machine, no real DVI, no parts inventory, no reminder delivery |
| Invoices, Quotes & Public Booking | [invoices-quotes-public-booking.md](invoices-quotes-public-booking.md) | Invoicing/GST/payment-ledger/PDF/sequence-numbering all solid; public booking API built but deliberately has no consuming UI yet |
| Appointments & Google Calendar Sync | [appointments-calendar-sync.md](appointments-calendar-sync.md) | One-way push to Google via service account works well; convert-on-arrival now fully atomic; no OAuth, no pull-back, no appointment reminders |
| Cash Flow & GST Reporting | [cash-flow-gst.md](cash-flow-gst.md) | Well-tested manual ledger, payees, rules, recurring auto-post, 4-source forecast; bank import/reconciliation/budgets/tax-obligations all unbuilt |
| Email Module | [email-module.md](email-module.md) | Outbound send (invoice/reminder/appointment), webhook, and templates all working; inbox mirror/IMAP and customer email timeline still unbuilt |
| Business Settings | [business-settings.md](business-settings.md) | Company profile/logo/bank-details plus configurable invoice/quote number prefix all working |
| Legacy Data Import | [legacy-data-import.md](legacy-data-import.md) | CLI importer verified idempotent against live DB; docs stale vs an undocumented real run; Phase 6 cutover not done |
| Infrastructure & Deployment | [infrastructure-deployment.md](infrastructure-deployment.md) | TLS live, nightly backup + real restore drill both verified; deploy is manual-SSH (no CI/CD); no monitoring/alerting/error-tracking |

## Known cross-cutting gaps (appear in multiple areas)
- **No delivery/notification layer beyond outbound email.** Reminders (job area) and appointment reminders (appointments area) have no SMS/push/email trigger at all — see `jobs-workshop-floor.md` §Reminders and `appointments-calendar-sync.md` §7.
- **CI/CD and observability are both fully absent** — no `.github/` workflows anywhere, no Sentry/APM/log-aggregation. See `infrastructure-deployment.md`.
- **Design/todo docs under `docs/` lag reality in both directions** — some (auth, email) describe *less* than what's built; others (cash-flow bank import) describe *more* than what's built. Don't trust them without cross-checking the relevant file here first.
