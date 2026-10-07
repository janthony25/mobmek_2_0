# Email Module

**Last verified:** 2026-10-07 (read against `Controllers/{EmailSettings,OutboundEmails}Controller.cs`, `Services/{ResendEmailSender,OutboundEmailService,EmailComposeService,EmailSettingsService,OutboundStatusPollJob}.cs`, `Entities/{EmailSettings,OutboundEmail,OutboundEmailKind,OutboundEmailStatus}.cs`, `docs/email-module-{design,todo}.md`)

**The todo doc's own header still says "Status: Not started" — that's stale.** Outbound invoice email is fully built and tested. Everything else in the design (templates, reminder/appointment email, IMAP inbox mirror) has zero code.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Outbound send (invoices only) | Working | `Services/OutboundEmailService.cs`, `Services/ResendEmailSender.cs` |
| Resend delivery webhook | **Missing** — polling only | — |
| Email templates | **Planned only** | no entity/service/UI exists |
| Delivery status tracking | Working, polling-based | `Services/OutboundStatusPollJob.cs` |
| Reminder / appointment email | **Not implemented** | no routes, no `OutboundEmailKind` values |
| Inbox mirror / IMAP | **Not implemented at all** | no MailKit dependency, no entities, no job |
| Settings / credentials UI | Working, outbound scope only | `Controllers/EmailSettingsController.cs`, `EmailSettingsPage.tsx` |
| Customer email timeline (merged sent+received) | **Not implemented** | — |

## Details

### Outbound send — Working (invoices only)
`OutboundEmailService.SendAsync` writes an `OutboundEmail` row with `Status=Queued` **before** calling the provider, then updates to `Sent`/`Failed`. Provider is a real Resend REST API call via typed `HttpClient` (`ResendEmailSender.cs`, POST to `api.resend.com`), **not SMTP**, single retry on 429/5xx. Triggered from exactly **one** place in the whole app: `InvoicesController.SendEmail`. Nothing in Jobs/Customers/Reminders/Appointments controllers sends email.

**Undocumented functional drift (an improvement, but unrecorded):** the design explicitly deferred PDF attachments to a later phase ("v1 sends a full HTML body instead"). Current code already attaches a real QuestPDF-generated PDF to every invoice email (`OutboundEmailService.BuildPdfAttachmentAsync`) — the design doc was never updated to reflect this.

Credentials: `Email:Resend:ApiKey` read live from config on every call, sourced from the `RESEND_API_KEY` env var — not cached, not DB-stored, nothing committed to `appsettings.json`. No secrets-in-repo issue found.

### Resend webhook — Missing
No `ResendWebhookController`, no signature verification, no `Email:Resend:WebhookSecret` reference anywhere. The design listed this as Phase 1 scope; it was skipped in favor of polling only (which the design did allow as a fallback — but the webhook half of "Phase 1 done" never shipped).

### Email templates — Planned only
No `EmailTemplate` entity, migration, controller, or token-substitution service anywhere. `EmailComposeService.BuildHtml` is fixed, hardcoded C# string-building — exactly the "Phase 1 fixed wording" the design describes, never upgraded. No template editor in the frontend.

### Delivery status tracking — Working, polling only
`OutboundStatusPollJob` is a registered `BackgroundService`, polls every 2 minutes for `Sent` rows under 72h old, applies results through a non-regressing state machine (a late "Delivered" can't overwrite a "Bounced"). `EmailStatusBadge.tsx` / `EmailHistoryList.tsx` render this correctly with retry support.

### Reminder / appointment email — Not implemented
Zero email references anywhere in `RemindersController.cs` or `AppointmentsController.cs`. No `POST .../reminders/{id}/email` or `.../appointments/{id}/email` route exists. Squarely Phase 2 design scope, not started.

### Inbox mirror / IMAP — 0% built
No `MailKit` package reference anywhere, no `InboundEmail`/`InboundEmailAttachment`/`EmailSyncState` entities, no `InboxSyncJob`. `EmailSettings` entity only has `FromName`/`FromAddress`/`ReplyToAddress`/`BccSelf` — none of the design's inbound fields (`InboundEnabled`, IMAP host/port/user, sync interval, retention days) were ever added, confirming this was never even scaffolded. No frontend route, viewer, or unread badge exists either. **This is the single biggest gap vs. the design doc** — the doc frames the inbox mirror as half the module's value proposition; zero of it exists.

### Settings / credentials UI — Working (outbound scope)
`EmailSettingsController` exposes GET/PUT for from-name/from-address/reply-to/BCC-self and a test-send endpoint; secrets never leave the backend (`ResendConfigured` boolean only, computed from both API-key presence **and** a non-blank From address — a documented bug fix: checking only the key used to show "Configured" while sends actually 422'd). No IMAP/inbound section in the UI (correctly, since nothing backs it). Route is `/email-settings`, not `/settings/email` as the design specifies — cosmetic drift only.

### Tests — present, outbound-only
`OutboundEmailServiceTests.cs` (16), `EmailComposeServiceTests.cs` (5), `EmailSettingsServiceTests.cs` (6), `ResendEmailSenderTests.cs` (10) — 781 lines total, including a `FakeEmailSender` that specifically asserts the "Queued row exists before provider call" invariant. No tests for templates, reminder/appointment sends, webhooks, or IMAP — consistent with those being unbuilt.

## Summary table (verbatim from audit)

| Sub-capability | Status | Key gap |
|---|---|---|
| Outbound send (invoices) | Working | Only trigger point is invoices; PDF-attachment approach is undocumented drift |
| Resend webhook | Missing | Poll job only |
| Email templates | Planned only | Wording hardcoded in C# |
| Delivery status tracking | Working | Polling-based, no real-time path |
| Reminder/appointment email | Not implemented | No routes, no enum values |
| Inbox mirror / IMAP | Not implemented | 0% — no dependency, entities, job, or UI |
| Settings/credentials UI | Working | No inbound/IMAP fields (nothing to configure yet) |
| Customer email timeline | Not implemented | Only per-document compose modals exist |

## Recommended follow-ups
- Fix the stale "Status: Not started" header in `docs/email-module-todo.md` — it undersells what's shipped.
- Update the design doc to reflect the PDF-attachment decision (QuestPDF is in, HTML-only was the old plan).
- If inbox mirroring is still wanted, it needs to start from zero — nothing to build on top of yet.
