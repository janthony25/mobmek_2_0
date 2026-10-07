# Email Module

**Last verified:** 2026-10-07 (read against `Controllers/{EmailSettings,OutboundEmails,EmailTemplates,ResendWebhook}Controller.cs`, `Services/{ResendEmailSender,ResendWebhookVerifier,OutboundEmailService,EmailComposeService,EmailSettingsService,EmailTemplateService,EmailTemplateRenderer,OutboundStatusPollJob,NzTime}.cs`, `Entities/{EmailSettings,EmailTemplate,OutboundEmail,OutboundEmailKind,OutboundEmailStatus}.cs`, `docs/email-module-{design,todo}.md`; live-verified via curl against the running dev stack)

**The todo doc's own header still says "Status: Not started" — that's stale** (not fixed in this change; tracked separately). Outbound invoice email, the Resend webhook, templates, and reminder/appointment email are all now built and tested. Only the IMAP inbox mirror (explicitly scoped out of this round — it needs a new `MailKit` dependency sign-off) and the merged customer email timeline remain unbuilt.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Outbound send (invoice/reminder/appointment) | Working | `Services/OutboundEmailService.cs`, `Services/ResendEmailSender.cs` |
| Resend delivery webhook | Working (fixed 2026-10-07) | `Controllers/ResendWebhookController.cs`, `Services/ResendWebhookVerifier.cs` |
| Email templates | Working (fixed 2026-10-07) | `Entities/EmailTemplate.cs`, `Services/EmailTemplateService.cs`, `Controllers/EmailTemplatesController.cs` |
| Delivery status tracking | Working — polling *and* webhook | `Services/OutboundStatusPollJob.cs`, `Controllers/ResendWebhookController.cs` |
| Reminder / appointment email | Working (fixed 2026-10-07) | `Controllers/RemindersController.cs`, `Controllers/AppointmentsController.cs` |
| Inbox mirror / IMAP | **Not implemented at all** | no MailKit dependency, no entities, no job — deliberately out of scope this round |
| Settings / credentials UI | Working, outbound scope only | `Controllers/EmailSettingsController.cs`, `EmailSettingsPage.tsx` |
| Customer email timeline (merged sent+received) | **Not implemented** | — |

## Details

### Outbound send — Working (invoice, reminder, appointment)
`OutboundEmailService.SendAsync` writes an `OutboundEmail` row with `Status=Queued` **before** calling the provider, then updates to `Sent`/`Failed`. Provider is a real Resend REST API call via typed `HttpClient` (`ResendEmailSender.cs`, POST to `api.resend.com`), **not SMTP**, single retry on 429/5xx.

**Fixed 2026-10-07 — email is no longer invoice-only.** `POST api/reminders/{id}/email` and `POST api/appointments/{id}/email` now exist alongside `InvoicesController.SendEmail`, each backed by a dedicated `EmailComposeService.Compose{Reminder,Appointment}EmailAsync` and `OutboundEmailService.Send{Reminder,Appointment}EmailAsync`. `OutboundEmail` gained `ReminderId`/`AppointmentId` FKs (migration `AddReminderAndAppointmentEmailLinks`) alongside the existing `InvoiceId`, and `OutboundEmailKind` gained `Reminder`/`Appointment` values. Appointment confirmations default to the linked customer's email, falling back to the soft-contact `ContactEmail` snapshot for an appointment not yet converted; appointment times are converted UTC → Pacific/Auckland for display via the new `NzTime` helper. Frontend: `ReminderEmailModal.tsx` (wired into `ReminderDetailsModal.tsx`) and `AppointmentEmailModal.tsx` (wired into `AppointmentDetailModal.tsx`), both mirroring the existing invoice `EmailComposeModal.tsx` shape. Live-verified via curl: both routes produce an `OutboundEmail` row with the correct `Kind`/`CustomerId`/`ReminderId`/`AppointmentId`, and a real Resend rejection (bad test domain) proves the provider call actually fires.

**Undocumented functional drift (an improvement, but unrecorded):** the design explicitly deferred PDF attachments to a later phase ("v1 sends a full HTML body instead"). Current code already attaches a real QuestPDF-generated PDF to every invoice email (`OutboundEmailService.BuildPdfAttachmentAsync`) — the design doc was never updated to reflect this. Reminder/appointment emails have no PDF to attach (nothing to render) and were never meant to.

Credentials: `Email:Resend:ApiKey` read live from config on every call, sourced from the `RESEND_API_KEY` env var — not cached, not DB-stored, nothing committed to `appsettings.json`. No secrets-in-repo issue found.

### Resend webhook — Working (fixed 2026-10-07)
`ResendWebhookController` (`POST api/webhooks/resend`, `[AllowAnonymous]` — this app's global fallback policy requires auth otherwise) verifies Resend's Svix-style signature (`ResendWebhookVerifier`: HMAC-SHA256 over `{svix-id}.{svix-timestamp}.{body}`, keyed by the base64 payload of `Email:Resend:WebhookSecret`, checked against every `v1,<base64>` entry in `svix-signature`) and maps `email.delivered`/`bounced`/`complained` onto the exact same no-regress status state machine the poll job uses, via a new `IOutboundEmailService.ApplyStatusByProviderMessageIdAsync`. Returns 404 when no secret is configured (feature off) — live-verified. The poll job (`OutboundStatusPollJob`) is unchanged and remains the real mechanism in practice since this app has no public HTTPS endpoint today; the webhook is ready for when one exists. 10 new tests (`ResendWebhookVerifierTests.cs` + 2 `OutboundEmailServiceTests.cs` cases).

### Email templates — Working (fixed 2026-10-07)
New `EmailTemplate` entity (`Key`/`Name`/`SubjectTemplate`/`BodyIntroTemplate`/`IsSystem`), 3 keys (`InvoiceSend`/`ReminderDue`/`AppointmentConfirmation`), get-or-create-seeded on first read (`EmailTemplateService` — same idiom as `GstSetting`/`BusinessDetails`, so a fresh production database needs no separate seed step). `EmailTemplateRenderer` does deterministic `{{Token}}` substitution (unknown token → empty, no scripting). `EmailTemplatesController`: `GET /api/email-templates`, `PUT /api/email-templates/{key}`, `POST /api/email-templates/{key}/preview` (dummy-token rendering), gated by `Permissions.ManageBusinessSettings`. `EmailComposeService.ComposeInvoiceEmailAsync` now renders the `InvoiceSend` template for subject + default intro (previously hardcoded); reminder/appointment compose use their own templates the same way. Frontend editor added to `EmailSettingsPage.tsx` (subject + intro fields per template, Save, Preview). Migration `AddEmailTemplate`. 15 new tests (`EmailTemplateServiceTests.cs`, `EmailTemplateRendererTests.cs`).

### Delivery status tracking — Working, polling *and* webhook
`OutboundStatusPollJob` is a registered `BackgroundService`, polls every 2 minutes for `Sent` rows under 72h old, applies results through a non-regressing state machine (a late "Delivered" can't overwrite a "Bounced"). The Resend webhook now feeds the identical state machine. `EmailStatusBadge.tsx` / `EmailHistoryList.tsx` render this correctly with retry support.

### Reminder / appointment email — Working (fixed 2026-10-07)
See "Outbound send" above — this was the Phase 2 gap, now closed for the send path. `ReminderDto`/`AppointmentDto` each gained a `CustomerEmail`/`CustomerEmail` field (mirroring `InvoiceDto`) so the frontend can prefill the "To" address correctly rather than leaving it blank or relying on a stale contact snapshot.

### Inbox mirror / IMAP — 0% built, deliberately out of scope this round
No `MailKit` package reference anywhere, no `InboundEmail`/`InboundEmailAttachment`/`EmailSyncState` entities, no `InboxSyncJob`. `EmailSettings` entity only has `FromName`/`FromAddress`/`ReplyToAddress`/`BccSelf` — none of the design's inbound fields (`InboundEnabled`, IMAP host/port/user, sync interval, retention days) were ever added, confirming this was never even scaffolded. No frontend route, viewer, or unread badge exists either. **This is the single biggest gap vs. the design doc** — the doc frames the inbox mirror as half the module's value proposition; zero of it exists. Explicitly deferred: it needs the `MailKit` dependency signed off first (per `CLAUDE.md`'s "ask before adding a dependency" rule) and is a large enough scope to warrant its own session.

### Settings / credentials UI — Working (outbound scope)
`EmailSettingsController` exposes GET/PUT for from-name/from-address/reply-to/BCC-self and a test-send endpoint; secrets never leave the backend (`ResendConfigured` boolean only, computed from both API-key presence **and** a non-blank From address — a documented bug fix: checking only the key used to show "Configured" while sends actually 422'd). No IMAP/inbound section in the UI (correctly, since nothing backs it). Route is `/email-settings`, not `/settings/email` as the design specifies — cosmetic drift only. The new email-templates editor lives on this same page.

### Tests — present, now covering webhook/templates/reminder/appointment too
`OutboundEmailServiceTests.cs` (now 27), `EmailComposeServiceTests.cs` (now 12), `EmailSettingsServiceTests.cs` (6), `ResendEmailSenderTests.cs` (10), `ResendWebhookVerifierTests.cs` (7, new), `EmailTemplateServiceTests.cs` (8, new), `EmailTemplateRendererTests.cs` (4, new) — including a `FakeEmailSender` that specifically asserts the "Queued row exists before provider call" invariant. No tests for IMAP — consistent with it being unbuilt.

## Summary table (verbatim from audit)

| Sub-capability | Status | Key gap |
|---|---|---|
| Outbound send (invoice/reminder/appointment) | Working | — |
| Resend webhook | Working | Still secondary to the poll job in practice (no public HTTPS endpoint) |
| Email templates | Working | — |
| Delivery status tracking | Working | — |
| Reminder/appointment email | Working | — |
| Inbox mirror / IMAP | Not implemented | 0% — no dependency, entities, job, or UI; needs its own session |
| Settings/credentials UI | Working | No inbound/IMAP fields (nothing to configure yet) |
| Customer email timeline | Not implemented | Only per-document compose modals exist |

## Recommended follow-ups
- Fix the stale "Status: Not started" header in `docs/email-module-todo.md` — it undersells what's shipped (left as-is this round; the user asked to skip legacy-import-adjacent doc work for now and this one's low-priority prose, not behavior).
- Update the design doc to reflect the PDF-attachment decision (QuestPDF is in, HTML-only was the old plan) and the templates/reminder/appointment send implementation details above.
- If inbox mirroring is still wanted, it needs to start from zero — nothing to build on top of yet, and needs the `MailKit` dependency signed off first.
- Customer email timeline (merged sent+received) still has nothing to show on the received side without the inbox mirror; the sent side could be built now (`OutboundEmail` already has `CustomerId`) but wasn't in this round's scope.
