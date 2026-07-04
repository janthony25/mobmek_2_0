# Email Module — Design Document (v1)

**Status:** Planned · **Date:** 2026-07-03
**Scope:** Outbound transactional email (invoices, reminders, appointment confirmations) with per-document delivery tracking, plus a read-only mirror of the workshop's inbox inside Mobmek so incoming mail is never missed.

**Why:** the workshop currently emails invoices by hand from Gmail and has no way to know, from inside Mobmek, whether an invoice reached the customer or whether a customer has replied. This module makes Mobmek the system of record for everything the business *sends* (with delivery proof per invoice/reminder) and gives staff full visibility of everything that *arrives* — without rebuilding an email client.

---

## 1. Provider decisions (settled 2026-07-03)

| Concern | Decision | Why |
|---|---|---|
| **Outbound sending** | **Resend** (REST API over `HttpClient`, no SDK dependency) | Permanent free tier (3,000/month — years of headroom for a single workshop), custom-domain sending with SPF/DKIM, delivery/bounce visibility via API + webhooks, clean API. SendGrid retired its free tier May 2025 ($19.95/mo minimum); Gmail API is built for user mailboxes, not system mail (OAuth token fragility, no bounce feedback, suspension risk for automated sending). |
| **Inbound (inbox mirror)** | **IMAP via MailKit** against the workshop's existing Gmail/Workspace mailbox | Works with both free Gmail (app password + 2FA) and Google Workspace; zero OAuth verification. The Gmail API alternative effectively requires Workspace (free-Gmail OAuth apps in testing mode expire refresh tokens every 7 days; publishing with Gmail restricted scopes triggers Google's paid CASA assessment). IMAP is provider-agnostic, so a later move to Workspace or another host changes nothing. |
| **Replies** | Stay in Gmail. Mobmek sets `Reply-To` on outbound mail to the workshop's real inbox; the Mail page links "Open in Gmail" per message. | Compose/reply from Mobmek means sent-folder sync, threading, and split conversation history — deferred (§10) until the read-only mirror proves insufficient. |
| **Upgrade path** | Everything provider-facing sits behind `IEmailSender` and `IInboxProvider` interfaces. | Swapping Resend→Postmark or IMAP→Gmail API is a new implementation + config change, not a rewrite. |

**Prerequisites (Phase 0, no code):** a domain for the business (sending as `@gmail.com` through Resend is not possible — DKIM requires domain ownership), Resend account with the domain verified (SPF/DKIM/DMARC records), and an app password on the Gmail account (2FA required). Optional but recommended: Google Workspace on the domain so the business address matches the sending domain.

---

## 2. Design principles

1. **Every send is audited before it happens.** An `OutboundEmail` row is written (status `Queued`) *before* the provider is called; the provider result updates it. No email leaves the system without a ledger entry, and the rendered HTML is snapshotted so we can always show exactly what the customer received.
2. **Secrets never touch the database or the repo.** The Resend API key, webhook signing secret, and IMAP app password live in configuration (`dotnet user-secrets` locally, environment variables in compose/production). The `EmailSettings` DB singleton holds only non-secret preferences (from-name, reply-to, sync toggle, IMAP host/username). The settings UI shows *whether* a secret is configured, never its value.
3. **The inbox mirror is read-only plus mark-as-read.** Mobmek displays, searches, and flags mail and syncs read state both ways; it never deletes, moves, labels, or sends replies. Gmail remains authoritative for the mailbox.
4. **Inbound HTML is hostile input.** Message bodies render only inside a sandboxed iframe (`sandbox` attribute, `srcDoc`, scripts blocked); they are never injected into the app DOM. Attachments download through our API, never hot-linked.
5. **Local-first operation.** Mobmek runs on localhost, so nothing may *depend* on inbound webhooks or Google push. Delivery status is polled from Resend's API by a background job; the inbox is polled over IMAP. The Resend webhook endpoint is still implemented (signature-verified) so a future public deployment upgrades to real-time for free.
6. **Follow the existing slice pattern.** Entity (`BaseEntity`) → `AppDbContext` config → DTO records → `I{X}Service`/`{X}Service` (scoped, returns DTOs, `AsNoTracking()` reads, `CancellationToken`) → thin controller → service-level xUnit tests → EF migration. Background work follows the `RecurringTransactionPostingJob` hosted-service pattern.
7. **Reuse what exists.** Letterhead comes from the `BusinessDetails` singleton; recipient defaults from `Customer.Email`; inbound attachments store through `IFileStorage`; invoice email bodies are built from the same invoice data the print page uses.

**New dependency requiring sign-off:** `MailKit` (backend, Phase 3) — the standard .NET IMAP/MIME library; no realistic alternative for IMAP. No new frontend dependencies anticipated (HTML isolation uses the native iframe sandbox, not a sanitizer lib).

---

## 3. Domain model

### 3.1 `EmailSettings` — singleton (pattern: `GstSetting`)

| Field | Type | Notes |
|---|---|---|
| `FromName` | string | display name, e.g. "Mobmek Workshop" |
| `FromAddress` | string | verified sender, e.g. `accounts@mobmek.co.nz` |
| `ReplyToAddress?` | string | the real inbox replies should land in (the Gmail address) |
| `BccSelf` | bool | copy every outbound email to `ReplyToAddress` so Gmail also holds a copy (default true) |
| `InboundEnabled` | bool | master switch for the inbox mirror |
| `ImapHost` | string | default `imap.gmail.com` |
| `ImapPort` | int | default 993 (SSL) |
| `ImapUsername?` | string | the mailbox address |
| `SyncIntervalMinutes` | int | default 2; floor 1 |
| `InboundRetentionDays?` | int | null = keep forever; otherwise purge mirrored rows older than N days (Gmail keeps the originals) |

Secrets (in configuration, not here): `Email:Resend:ApiKey`, `Email:Resend:WebhookSecret?`, `Email:Imap:Password`. The settings DTO exposes `ResendConfigured` / `ImapPasswordConfigured` booleans computed from config presence.

### 3.2 `OutboundEmail` — one row per send attempt

| Field | Type | Notes |
|---|---|---|
| `ToAddress` | string | |
| `ToName?` | string | |
| `CcAddresses?` | string | comma-separated, small N |
| `Subject` | string | |
| `BodyHtml` | string | rendered snapshot of what was sent |
| `Status` | string | `Queued` → `Sent` → `Delivered` \| `Bounced` \| `Complained`; `Queued` → `Failed` (provider rejected). Terminal: Delivered/Bounced/Complained/Failed |
| `ProviderMessageId?` | string | Resend email id; set on accept; drives status polling |
| `ErrorMessage?` | string | provider error on `Failed`, bounce reason on `Bounced` |
| `SentAtUtc?` / `DeliveredAtUtc?` / `FailedAtUtc?` | DateTime | event timestamps |
| `Kind` | string | `Invoice` \| `Reminder` \| `Appointment` \| `Test` — what triggered it |
| `CustomerId?` | Guid FK | recipient customer, when known |
| `InvoiceId?` / `ReminderId?` / `AppointmentId?` | Guid FK | at most one set, per `Kind`; drives the "emailed ✓ delivered" badge on that document |

Rows are immutable after reaching a terminal status except via the status-update path. "Resend" (retry) creates a **new** row — history is never overwritten.

### 3.3 `EmailTemplate` — editable outbound wording (Phase 2)

| Field | Type | Notes |
|---|---|---|
| `Key` | string | unique: `InvoiceSend`, `ReminderDue`, `AppointmentConfirmation` (seeded, `IsSystem`, non-deletable) |
| `Name` | string | display |
| `SubjectTemplate` | string | token syntax `{{CustomerName}}`, `{{InvoiceNumber}}`, `{{DueDate}}`, `{{CarPlate}}`, `{{BusinessName}}`, … |
| `BodyIntroTemplate` | string | free-text paragraph above the generated document block |
| `IsSystem` | bool | seeded rows: editable wording, fixed key, no delete |

Token replacement is a small deterministic substitution service — no scripting, unknown tokens render empty and are reported by a `preview` endpoint. The invoice/reminder *document block* (line items, totals, bank details from `BusinessDetails`) is composed in C# by `EmailComposeService`, not user-editable.

### 3.4 `InboundEmail` — one mirrored inbox message

| Field | Type | Notes |
|---|---|---|
| `ImapUid` | long | UID within the mailbox |
| `UidValidity` | long | mailbox generation the UID belongs to |
| `MessageId?` | string | RFC 5322 `Message-ID` header — dedup across UIDVALIDITY resets |
| `FromAddress` / `FromName?` | string | |
| `ToAddresses?` / `CcAddresses?` | string | |
| `Subject?` | string | |
| `Snippet` | string | first ~160 chars of plain text, for the list view |
| `BodyHtml?` / `BodyText?` | string | at least one present; HTML stored raw, rendered sandboxed (§2.4) |
| `ReceivedAtUtc` | DateTime | from the message date |
| `IsRead` | bool | mirrored two-way with the Gmail `\Seen` flag |
| `HasAttachments` | bool | |
| `CustomerId?` | Guid FK | matched on `FromAddress` = `Customer.Email` (case-insensitive) at sync time |

Unique index `(UidValidity, ImapUid)`; secondary dedup on `MessageId`.

**`InboundEmailAttachment`** — child rows: `InboundEmailId`, `FileName`, `ContentType`, `SizeBytes`, `StoragePath` (via `IFileStorage`). Bodies >2 MB of attachments are fetched lazily on first open rather than at sync time.

### 3.5 `EmailSyncState` — singleton sync cursor

`LastUidValidity`, `LastSeenUid`, `LastSyncAtUtc`, `LastSyncError?`, `ConsecutiveFailures`. One mailbox in v1; keyed singleton so multi-mailbox later is additive.

---

## 4. Services & background jobs

| Component | Responsibility |
|---|---|
| `IEmailSender` / `ResendEmailSender` | POST `https://api.resend.com/emails` via typed `HttpClient`; returns provider id or a typed failure. Also `GetStatusAsync(providerId)` for the poll job. Retries once on 429/5xx with backoff; never retries 4xx validation errors. |
| `IEmailComposeService` / `EmailComposeService` | Builds subject + HTML for each `Kind`: letterhead from `BusinessDetails` (name, logo, GST number, bank details), document block from the invoice/reminder/appointment, intro paragraph from the template with tokens substituted. Pure function of its inputs → easily unit-tested. |
| `IOutboundEmailService` | The send pipeline: validate settings + recipient → write `Queued` row → call `IEmailSender` → update to `Sent`/`Failed`. Paged history queries with filters (`customerId`, `invoiceId`, `status`, `kind`). Test-send. Retry-as-new-row. |
| `OutboundStatusPollJob` (hosted) | Every 2 min: for rows in `Sent` younger than 72 h, `GetStatusAsync` → apply `Delivered`/`Bounced`/`Complained`. Rows still `Sent` after 72 h stay `Sent` (delivered-unconfirmed). Skips entirely when no rows qualify. |
| `ResendWebhookController` | `POST api/webhooks/resend` — Svix-style signature verification against `Email:Resend:WebhookSecret`; maps `email.delivered` / `email.bounced` / `email.complained` events onto the same status-update path the poll job uses. Returns 404 when no secret configured (feature off). |
| `IInboxProvider` / `ImapInboxProvider` | MailKit wrapper: connect/auth, read `UIDVALIDITY`, fetch summaries + bodies for UIDs > cursor, fetch `\Seen` flag changes for a recent window, set `\Seen`. All IMAP specifics stay inside this class. |
| `IInboundEmailService` | Paged inbox queries (filters: `unread`, `customerId`, `search` over from/subject/snippet), detail, mark read/unread (DB + write-back through provider), unread count, attachment streaming, customer re-match. |
| `InboxSyncJob` (hosted) | The sync loop (§5) on `SyncIntervalMinutes`; no-ops when `InboundEnabled` is false or IMAP password missing; records errors in `EmailSyncState` instead of throwing. |

Controllers stay thin per house rules; `POST api/invoices/{id}/email` lives on `InvoicesController` (and likewise reminders/appointments) so the document id is the route anchor.

### Endpoints

| Endpoint | Purpose |
|---|---|
| `GET/PUT api/emailsettings` | singleton settings (secrets excluded; `*Configured` flags included) |
| `POST api/emailsettings/test` | send a test email to a given address; returns the `OutboundEmail` row |
| `POST api/invoices/{id}/email` | body: `to`, `cc?`, `subject`, `intro` (pre-filled from template; editable per send) |
| `POST api/reminders/{id}/email` · `POST api/appointments/{id}/email` | same shape (Phase 2) |
| `GET api/outbound-emails` | paged; filters `customerId`, `invoiceId`, `reminderId`, `status`, `kind` |
| `POST api/outbound-emails/{id}/retry` | failed/bounced only; creates a new row |
| `GET api/outbound-emails/{id}/preview` | the stored `BodyHtml` for display |
| `POST api/webhooks/resend` | signature-verified status events |
| `GET/PUT api/email-templates` | list + edit wording (Phase 2) |
| `POST api/email-templates/{key}/preview` | rendered sample with dummy tokens |
| `GET api/inbound-emails` | paged; filters `unread`, `customerId`, `search` |
| `GET api/inbound-emails/{id}` | full detail incl. body |
| `POST api/inbound-emails/{id}/read` · `/unread` | two-way read state |
| `GET api/inbound-emails/unread-count` | drives the sidebar badge |
| `GET api/inbound-emails/{id}/attachments/{attId}` | streamed download |
| `POST api/inbound-emails/sync` | manual "check now"; returns sync result summary |

---

## 5. Inbox sync algorithm (IMAP, incremental)

1. Connect, open INBOX read-write, read `UIDVALIDITY`.
2. **Cursor valid** (`UIDVALIDITY` matches stored): fetch envelopes + bodies for `UID > LastSeenUid`, insert `InboundEmail` rows (customer-match on insert), advance cursor. New unread rows feed the badge/notification.
3. **Cursor invalid** (mailbox reset — rare): full envelope walk of the last `InboundRetentionDays` (or 90 days default), dedup against existing rows by `MessageId`, rebuild `(UidValidity, Uid)` pairs, reset cursor.
4. **Read-state reconcile**: fetch `\Seen` flags for messages received in the last 30 days; where Gmail differs from Mobmek, Gmail wins for changes made *in Gmail* (flag differs from our last-known), Mobmek wins for pending local marks — practically: apply remote flag unless a local mark-as-read is newer than the last sync.
5. Mark-as-read from Mobmek writes the DB immediately and queues the `\Seen` write-back; a failed write-back retries on the next sync (idempotent).
6. Errors set `LastSyncError`/`ConsecutiveFailures` (surfaced in the Mail page header as "last checked X min ago / sync failing"); the job backs off exponentially after 3 consecutive failures, capped at 30 min.

Attachments: metadata stored at sync; content fetched and cached through `IFileStorage` on first download request.

---

## 6. Frontend surfaces

| Surface | Content |
|---|---|
| **Mail page** (`/mail`, new sidebar entry) | Two-pane inbox: paged list (unread bold, sender, subject, snippet, time, customer chip when matched, attachment icon) + detail pane (sandboxed-iframe body, attachment list, mark read/unread, "Open in Gmail" deep link `https://mail.google.com/mail/#search/rfc822msgid:{MessageId}`). Header: unread count, "Check now" button, last-sync status. Filters: unread, customer, search. |
| **Sidebar badge** | Unread count on the Mail entry; `unread-count` polled every 60 s app-wide; in-app toast on count increase ("New email from …"). |
| **Invoice surfaces** (JobDetailPage invoice section / invoice modal) | "Email invoice" button → compose modal (to/cc pre-filled from customer, subject + intro pre-filled from template, document preview) → send. Status badge on the invoice: `Emailed ✓ Delivered` / `Sent` / `Bounced ✗` (bounce shows reason + prompts a phone call). Email history list per invoice with per-row preview. |
| **Reminders / appointments** (Phase 2) | Same compose-modal pattern from ReminderCard / appointment details. |
| **CustomerDetailPage** | "Emails" section: merged timeline of sent (`OutboundEmail` by `CustomerId`) and received (`InboundEmail` matched) — the conversation record next to the customer. |
| **Email Settings page** (`/settings/email`) | From-name/address, reply-to, BCC-self, template wording editor (Phase 2), inbound toggle + IMAP host/username + interval, secret-configured indicators with setup hints, test-send button. |

Shared components: `EmailComposeModal`, `EmailStatusBadge`, `EmailHistoryList`, `InboundEmailViewer` (the sandboxed iframe wrapper) under `src/components/email/`; API modules `src/api/emailSettings.ts`, `outboundEmails.ts`, `inboundEmails.ts`, `emailTemplates.ts`; types in `src/types/index.ts`.

---

## 7. Security & operational notes

- **Sandboxing:** inbound HTML renders via `<iframe sandbox srcDoc={bodyHtml}>` — no scripts, no top-navigation, no form posts. The app DOM never receives message HTML. Remote images load inside the sandbox (acceptable v1; a "block remote images" toggle is a fast-follow).
- **Secrets:** user-secrets in dev, environment variables in compose. Startup logs a clear one-line warning per missing secret; send/sync endpoints return a typed `EmailNotConfigured` error the UI turns into a setup hint.
- **Rate limits:** Resend free tier ≈ 2 req/s — irrelevant at workshop volume, but `ResendEmailSender` still serialises sends and honours 429 backoff.
- **Failure visibility:** a bounced invoice email is a business event, not a log line — the invoice badge goes red and the job/invoice view says why.
- **Data growth:** inbox mirror is bounded by `InboundRetentionDays` purge (Gmail keeps originals); `OutboundEmail` is kept forever (it is the audit record of what was sent).
- **Idempotency:** webhook and poll updates are last-write-wins onto a status state machine that never regresses (e.g. a late `delivered` event cannot overwrite `Bounced`).

---

## 8. Phases & expected output

Each phase is independently shippable and leaves the app fully working. Detailed checklists live in [`email-module-todo.md`](./email-module-todo.md).

| Phase | Delivers | Observable output when done |
|---|---|---|
| **0 — Accounts & DNS** (no code) | Domain, Resend domain verification, Gmail app password, secrets stored | A test email sent from the Resend dashboard lands in a personal inbox from `accounts@<domain>`; IMAP login verified with the app password |
| **1 — Outbound core: invoice emails** | Settings singleton + page, `OutboundEmail`, `ResendEmailSender`, compose service, invoice send endpoint + poll job + webhook, compose modal + status badge + history on invoices | Staff email a real invoice from the job page; the invoice shows `Emailed ✓ Delivered` within minutes; a deliberately bad address shows `Bounced ✗` with reason; test-send works from the settings page |
| **2 — Templates & outbound everywhere** | `EmailTemplate` (seeded ×3) + editor + preview, reminder & appointment send endpoints + UI, customer email timeline (sent side), retry | Reminder and appointment emails send with editable wording; CustomerDetailPage lists everything sent to that customer; failed sends can be retried |
| **3 — Inbox mirror (read-only)** | MailKit, `InboundEmail` + attachments + sync state, `ImapInboxProvider`, `InboxSyncJob`, inbound endpoints, Mail page, sidebar unread badge | The workshop's Gmail inbox appears in Mobmek within ~2 min of arrival; unread badge counts correctly; reading in Mobmek marks read in Gmail and vice versa; customer emails carry a customer chip; attachments download |
| **4 — Awareness & polish** | New-mail toast, inbox search/filters, received side of the customer timeline, sync-health surfacing, bounced-invoice alert on job views | Staff working anywhere in Mobmek get a toast when mail arrives; the customer page shows the full two-way email history; a failing sync is visible without checking logs |
| **5 — Deferred** (§10) | — | — |

---

## 9. Testing strategy

- **Service tests (xUnit, in-memory `AppDbContext`)**, same-change rule per repo convention. Provider interfaces get fakes: `FakeEmailSender` (scripted accept/reject/status sequences), `FakeInboxProvider` (scripted mailbox states incl. UIDVALIDITY reset).
- Key coverage: send pipeline writes `Queued` before provider call and `Failed` on rejection; status state machine never regresses; retry creates a new row; compose output contains letterhead/lines/totals/bank details and substitutes tokens (unknown token → empty + reported in preview); poll job selects only non-terminal <72 h rows and is idempotent; webhook rejects bad signatures; sync advances cursor, dedups on UIDVALIDITY reset via `MessageId`, matches customers case-insensitively, reconciles read flags per §5.4; unread count respects retention purge; settings never expose secrets.
- **Frontend gate:** `npx tsc -b` + `npm run lint` clean per change; live browser pass per phase before it's called shipped.

---

## 10. Deferred (explicitly out of v1)

- **Reply/compose from Mobmek** — needs `In-Reply-To`/`References` threading, sent-copy strategy, and a decision on where conversation history lives.
- **Gmail API `IInboxProvider`** — worth it only after Workspace exists; brings push notifications (needs public endpoint) and label support.
- **PDF invoice attachments** (e.g. QuestPDF — new dependency, needs sign-off); v1 sends a full HTML invoice body instead.
- **Auto-send** (reminder emails on due date, invoice chasing) — send stays a human action until trust in deliverability is established.
- **Open/click tracking, marketing/bulk email, multiple mailboxes, block-remote-images toggle.**
