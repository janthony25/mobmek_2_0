# Email Module — Implementation Checklist (v1)

Tracks delivery of [`email-module-design.md`](./email-module-design.md). Section references (§) point into the design doc.

**Status:** Not started · plan written 2026-07-03. Phases ship in order; each leaves the app fully working. Phase 3 needs the `MailKit` dependency signed off first (§2).

---

## Phase 0 — Accounts & DNS (no code, business owner + operator)

- [ ] Buy/confirm the business domain (needed for DKIM — cannot send as `@gmail.com` via Resend) (§1)
- [ ] Create Resend account → add domain → set SPF/DKIM/DMARC DNS records → verified status
- [ ] Create Resend API key (send scope); store via `dotnet user-secrets set "Email:Resend:ApiKey" …` locally + env var in compose
- [ ] Gmail: enable 2FA → generate app password → store as `Email:Imap:Password` (same two places)
- [ ] Decide reply-to inbox address (the existing Gmail) and desired from-address (e.g. `accounts@<domain>`)
- [ ] *(Optional, recommended)* Google Workspace on the domain so the mailbox matches the sending domain
- [ ] **Expected output:** test email from the Resend dashboard lands in a personal inbox from the business domain (not spam); IMAP login with the app password verified (any IMAP client or a one-off script)

---

## Phase 1 — Outbound core: invoice emails (§3.1, §3.2, §4, §8)

### Backend
- [ ] `EmailSettings` singleton entity + `IEmailSettingsService` (get-or-create like `GstSetting`) + `EmailSettingsController` — DTO excludes secrets, includes `ResendConfigured`/`ImapPasswordConfigured` flags from config (§3.1)
- [ ] `OutboundEmail` entity + `AppDbContext` config (FKs to Customer/Invoice; indexes on `Status`, `InvoiceId`, `CustomerId`) (§3.2)
- [ ] `IEmailSender` / `ResendEmailSender` — typed `HttpClient`, `SendAsync` + `GetStatusAsync`, single retry on 429/5xx, typed failure on 4xx; registered via `AddHttpClient` (§4)
- [ ] `IEmailComposeService` / `EmailComposeService` — invoice subject + HTML body: `BusinessDetails` letterhead (name, logo, GST number, bank details), line items, totals, due date; fixed wording v1 (templates are Phase 2) (§4)
- [ ] `IOutboundEmailService` — send pipeline (`Queued` row **before** provider call → `Sent`/`Failed`), paged history with filters, test-send, `retry` (new row, failed/bounced only), `preview` (stored `BodyHtml`) (§4)
- [ ] `POST api/invoices/{id}/email` on `InvoicesController` (to/cc/subject/intro; recipient pre-fill from customer) + `GET api/outbound-emails` + `POST …/retry` + `GET …/preview` + `POST api/emailsettings/test`
- [ ] `OutboundStatusPollJob` (hosted, pattern: `RecurringTransactionPostingJob`) — every 2 min, `Sent` rows <72 h, status state machine never regresses (§4)
- [ ] `ResendWebhookController` — signature verification, 404 when no `WebhookSecret` configured, same status-update path as the poll job (§4)
- [ ] `EmailNotConfigured` typed error when API key/from-address missing — UI turns it into a setup hint (§7)
- [ ] Migration `AddOutboundEmail` + DI registrations
- [ ] Service tests (`FakeEmailSender`): queued-before-send, failure paths, state machine no-regress, retry-as-new-row, compose content (letterhead/lines/totals/bank details), poll selection + idempotency, webhook signature reject, settings secret exclusion (§9)

### Frontend
- [ ] Types + API modules: `emailSettings.ts`, `outboundEmails.ts`; invoice email call in `invoices.ts`
- [ ] `components/email/`: `EmailComposeModal` (to/cc/subject/intro + document preview), `EmailStatusBadge`, `EmailHistoryList` (per-row preview open)
- [ ] "Email invoice" button + modal on JobDetailPage invoice section; status badge (`Emailed ✓ Delivered` / `Sent` / `Bounced ✗` + reason) + history per invoice
- [ ] **Email Settings page** (`/settings/email`, sidebar entry): from/reply-to/BCC-self form, secret-configured indicators with setup hints, test-send button
- [ ] Gate: `npx tsc -b` + `npm run lint` clean
- [ ] **Expected output (live pass):** email a real invoice to a test address → badge reaches `Delivered` within minutes; send to a bad address (`bounce@resend.dev`) → `Bounced ✗` with reason; missing API key → setup hint, not a crash

---

## Phase 2 — Templates & outbound everywhere (§3.3, §4, §8)

### Backend
- [ ] `EmailTemplate` entity — seeded `InvoiceSend` / `ReminderDue` / `AppointmentConfirmation`, `IsSystem` (wording editable, key fixed, no delete) (§3.3)
- [ ] Token substitution service — `{{CustomerName}}`, `{{InvoiceNumber}}`, `{{DueDate}}`, `{{CarPlate}}`, `{{BusinessName}}`…; unknown tokens → empty + reported by preview (§3.3)
- [ ] `EmailComposeService` reads templates for subject/intro; Phase 1 fixed wording becomes the seed content
- [ ] `POST api/reminders/{id}/email` + `POST api/appointments/{id}/email` (compose from reminder/appointment + customer/car context)
- [ ] `GET/PUT api/email-templates` + `POST api/email-templates/{key}/preview` (dummy-token render)
- [ ] Migration `AddEmailTemplates` + seeding + tests (token edge cases, per-kind compose, system-template guards)

### Frontend
- [ ] "Email reminder" on `ReminderCard`/details, "Email confirmation" on appointment details — shared `EmailComposeModal`
- [ ] Template wording editor + live preview on the Email Settings page
- [ ] CustomerDetailPage "Emails" section — sent timeline (`outbound-emails?customerId=`) with status badges (received side lands in Phase 4)
- [ ] Gate: tsc + lint
- [ ] **Expected output:** reminder + appointment emails send with per-send editable wording; template edits show in preview and next send; customer page lists everything sent to them; a failed send retries from the history list

---

## Phase 3 — Inbox mirror, read-only (§3.4, §3.5, §5, §8)

> Requires: `MailKit` dependency sign-off; Phase 0 IMAP items done.

### Backend
- [ ] Add `MailKit`; `IInboxProvider` / `ImapInboxProvider` — connect/auth, `UIDVALIDITY`, fetch summaries + bodies above cursor, `\Seen` flag window reads, `\Seen` write-back; all IMAP detail contained here (§4)
- [ ] `InboundEmail` + `InboundEmailAttachment` + `EmailSyncState` entities; unique `(UidValidity, ImapUid)`; `MessageId` dedup index (§3.4, §3.5)
- [ ] `InboxSyncJob` (hosted) — §5 algorithm: incremental fetch, UIDVALIDITY-reset resync with `MessageId` dedup, read-state reconcile (30-day window, rules per §5.4), queued `\Seen` write-backs with retry, error capture + exponential backoff after 3 failures, no-op when disabled/unconfigured
- [ ] Customer matching on insert (`FromAddress` vs `Customer.Email`, case-insensitive) + `re-match` endpoint for after customer edits
- [ ] Attachment content lazy-fetch through `IFileStorage` on first download (§5)
- [ ] Retention purge (`InboundRetentionDays`) inside the sync job
- [ ] `IInboundEmailService` + `InboundEmailsController` — paged list (`unread`/`customerId`/`search`), detail, read/unread, unread-count, attachment stream, manual `sync` (§4 endpoints)
- [ ] `EmailSettings` gains the inbound fields (host/port/username/interval/retention/enabled) — extend Phase 1 page + DTO
- [ ] Migration `AddInboundEmail` + DI
- [ ] Service tests (`FakeInboxProvider`): cursor advance, UIDVALIDITY reset dedup, read reconcile both directions incl. pending-local-mark precedence, write-back retry idempotency, matcher casing, unread count vs purge, backoff after failures (§9)

### Frontend
- [ ] Types + `inboundEmails.ts`
- [ ] **Mail page** (`/mail` + sidebar entry): two-pane list/detail — unread bold, customer chip, attachment icon; detail = `InboundEmailViewer` (**sandboxed iframe `srcDoc`, scripts blocked — never inject message HTML into the app DOM**, §7), attachment downloads, mark read/unread, "Open in Gmail" (`rfc822msgid:` deep link); header: unread count, "Check now", last-sync status/error
- [ ] Sidebar unread badge — `unread-count` polled every 60 s app-wide
- [ ] Filters: unread toggle, customer, search
- [ ] Gate: tsc + lint
- [ ] **Expected output (live pass):** send an email to the workshop Gmail from outside → appears in Mobmek ≤2 min, badge increments; open it → marked read in Gmail; read in Gmail → cleared in Mobmek next sync; email from a known customer address shows the chip; attachment downloads; stopping the network shows a sync-failing header, not a broken page

---

## Phase 4 — Awareness & polish (§6, §8)

- [ ] In-app toast on unread-count increase ("New email from …") wherever staff are in the app
- [ ] CustomerDetailPage timeline gains the received side — merged sent + received, newest first
- [ ] Bounced invoice email surfaced on JobDetailPage/job views (red badge + reason: "invoice didn't reach the customer — call them")
- [ ] Sync health: settings page shows last sync, consecutive failures, next attempt
- [ ] Mail page niceties: date grouping, unread-only default toggle memory, empty/first-run states with setup guidance
- [ ] Gate: tsc + lint + live pass
- [ ] **Expected output:** a customer reply triggers a toast while staff work elsewhere in Mobmek; the customer page reads as a conversation history; a silently failing sync is visible in the UI without checking logs

---

## Phase 5 — Deferred (§10)
- [ ] Reply/compose from Mobmek (threading headers, sent-copy strategy)
- [ ] Gmail API `IInboxProvider` (after Workspace; push notifications need a public endpoint)
- [ ] PDF invoice attachments (QuestPDF — dependency sign-off)
- [ ] Auto-send (reminders on due date, invoice chasing)
- [ ] Open/click tracking · multiple mailboxes · block-remote-images toggle

---

## Cross-cutting reminders
- Secrets (`Email:Resend:ApiKey`, `Email:Resend:WebhookSecret`, `Email:Imap:Password`) live in user-secrets/env only — never in the DB, DTOs, or repo; UI sees only `*Configured` booleans (§2.2).
- Every send writes its `OutboundEmail` row **before** the provider call; retry = new row; status machine never regresses (§2.1, §7).
- Inbound HTML renders only inside the sandboxed iframe; attachments only via our API (§2.4, §7).
- The mirror is read-only + mark-as-read — no delete/move/label/send against the mailbox (§2.3).
- Nothing depends on webhooks/push (localhost) — polling is the baseline, webhooks an upgrade (§2.5).
- Every backend item ships with service-level xUnit tests in the same change (fakes: `FakeEmailSender`, `FakeInboxProvider`); frontend gate is `npx tsc -b` + `npm run lint`; each phase gets a live browser pass before it's called shipped.
- New dependencies need explicit sign-off: `MailKit` (Phase 3) anticipated; no new frontend deps expected.
