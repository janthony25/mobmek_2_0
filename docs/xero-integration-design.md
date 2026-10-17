# Xero Integration — Design Document (v1)

**Status:** Planned · **Date:** 2026-10-17
**Scope:** push generated invoices from Mobmek into Xero, and sync payment/paid status back — so Mobmek stays the job-and-document-creation front end, while Xero becomes the business-overview and bookkeeping system of record (reconciliation, payments, accountant-facing reporting).

**Why:** the business already pays for Xero. Today an invoice generated in Mobmek has no footprint there at all — the owner's accountant and the owner's own "how's the business doing" view live entirely outside this app, manually re-entered. This module removes that gap for the sales side first, without touching how jobs/invoices are created today.

---

## 0. What "full visibility in Xero" actually requires

This plan's v1 is **invoices + two-way paid status only**, by deliberate choice (§1). For the fuller question — what else would it take for Xero to show the *whole* business — here's the honest map, so scope decisions later are made with the full picture, not discovered one gap at a time:

| Area | Needed for full visibility? | Status in this plan |
|---|---|---|
| Sales invoices (and their lines, GST, customer) | Yes | **In scope — Phase 1** |
| Payment / paid status, both directions | Yes | **In scope — Phase 2** |
| Customer → Xero Contact | Yes, but it's a *mechanical dependency* of invoices, not a separate visibility win | Built as part of Phase 1, not tracked as its own milestone |
| Quotations (Xero "Quotes") | Nice-to-have — quotes aren't money yet | **Deferred** — separate Xero API object, no business urgency |
| Bank feeds | Yes — this is *how* reconciliation happens | **Not our work** — the owner connects the shop's real bank account directly inside Xero; nothing in this app touches it |
| Expenses / supplier bills (today: manual `CashTransaction` rows in our own Cash Flow ledger) | Yes, for a true P&L in Xero | **Out of scope v1** — see open question below |
| Chart of Accounts mapping (our `TransactionCategories` ↔ Xero's COA) | Only once expenses/bills sync | **Out of scope v1** |
| GST return filing (Xero can file GST101A straight to IR once the ledger is reconciled there) | Eventually, if Xero becomes the real ledger | **Out of scope v1** — our GST Report stays "review only," unchanged |
| Credit notes / refunds | Yes, once a paid invoice needs reversing | **Out of scope v1** — no in-app concept of this today either |
| Payroll (Xero Payroll NZ) | Only if/when the shop has PAYE staff | **Out of scope**, not triggered by anything in this app |
| Fixed assets / depreciation | Only once equipment purchases are tracked somewhere | **Out of scope** |
| Multi-currency | No — NZ-only shop | N/A |

**Open strategic question this plan does *not* resolve (flagging, not deciding):** the in-house Cash Flow module's own next phase (bank-statement import + reconciliation — see `cash-flow-gst.md`) is now *the same problem* Xero already solves well. Once Phase 2 of this plan ships (§5) and invoice payments are reconciled in Xero, building a competing bank-import/reconciliation feature in-house would be duplicated effort. **Recommendation: shelve the in-house bank-import/reconciliation phase and let Xero own that**, but keep the existing ledger (`CashTransaction`, categories, forecast) running as-is until Xero has been the real workflow for a few months and the owner is confident retiring that half is safe. Revisit explicitly before starting any new Cash Flow v2 work.

---

## 1. Decisions (settled 2026-10-17)

| Concern | Decision | Why |
|---|---|---|
| **Mobmek's role going forward** | Mobmek stays **job-centric**: jobs, invoices, and quotes are still created and edited here. Xero becomes the **business/bookkeeping overview** — reconciliation, "is this actually paid," accountant-facing reports. | Owner's explicit call: "system would now be job centric... all the data and business overview would be on Xero." |
| **Invoice lifecycle direction** | **One-way, Mobmek → Xero.** Generate/reject in Mobmek pushes to Xero; Xero is never the place an invoice is created, edited, or voided from. | Mobmek owns job→invoice generation; duplicating that authority in Xero would mean two sources of truth for line items/GST. |
| **Payment status direction** | **Two-way.** A payment applied in Xero (via bank reconciliation or manual entry there) flows back and marks the Mobmek invoice paid; a payment recorded in Mobmek (Mark Paid / Partial Payment) pushes to Xero as a `Payment` against the mirrored invoice. | Owner's explicit ask — "if its mark paid in Xero it should be mark paid in system as well." This is the whole point of leaning on Xero for reconciliation. |
| **Contacts** | Auto-create/match a Xero Contact per Mobmek `Customer` the first time one of their invoices syncs (match by email, else create). One-way, Mobmek → Xero. Not a separate phase or sync target — purely what Xero's Invoice API requires. | Xero invoices can't exist without a Contact; no independent value in syncing contacts that never generate an invoice. |
| **v1 scope** | **Invoices only** (not quotations, not the wider ledger). | Owner's explicit call: "Just invoices for now." Quotations and ledger/expense mirroring are real scoping exercises of their own (§0). |
| **Auth method** | **Standard OAuth2 Authorization Code flow + `offline_access`** (user consents once in a browser; app keeps a refresh token alive). | Owner has a Xero org but hasn't touched the developer/API side — this is the standard path for a single-organisation connection, same shape as any "Connect your Xero" button. A Custom Connection (machine-to-machine, no consent screen) is a plan-gated alternative noted in §6 if the owner's Xero plan supports it later. |
| **Where invoices live once generated** | Xero Invoice stays a **mirror keyed by `Invoice.XeroInvoiceId`**; Mobmek remains the only place that can create/edit/void. | Keeps the "who's allowed to create the document" rule unambiguous even though payment status flows back. |
| **Rate limits / delivery** | **Durable outbox + hosted background job**, same pattern as `CalendarSyncItem`/`CalendarSyncJob` — never call Xero inline in a request. | Xero enforces ~60 calls/min and 5,000/day per connection; an outbox naturally throttles and survives restarts/outages without ever blocking an invoice-generation request. |

**New dependency requiring sign-off:** an official Xero .NET SDK (`Xero.NetStandard.OAuth2Client` + the Xero Accounting API client) or a thin hand-rolled HTTP client against the Xero Accounting API — to be confirmed in Phase 0 based on how well-maintained the official SDK is at implementation time.

---

## 2. Design principles

1. **Sync failures are invisible to the invoice-creation flow.** Generating or rejecting an invoice in Mobmek always succeeds in exactly the time Postgres takes; the Xero mirror catches up seconds to minutes later via the outbox.
2. **Unconfigured/disconnected = cleanly disabled.** No Xero connection → push hooks and webhook processing no-op with a startup/runtime log line, same graceful-degradation pattern as Resend and Google Calendar. Nothing else in the app changes.
3. **Secrets never touch the repo.** OAuth client id/secret and the webhook signing key live in config/env (`dotnet user-secrets` locally, env in compose) — same pattern as `Email:Resend:ApiKey`. The **refresh token is different**: it's not a static secret, it's live state that rotates on every use and must be persisted in Postgres, encrypted at rest (ASP.NET Core Data Protection) — this is the one real deviation from the Google Calendar pattern's "config-only" secret model, because Xero's OAuth tokens actually change over time where a GCal service-account key never does.
4. **A Xero-side change always wins for payment status.** Once an invoice is mirrored, Xero is authoritative for "is it paid / how much" — it's where reconciliation now happens. A Mobmek-originated payment is *pushed*, not locally authoritative, until Xero accepts it.
5. **Follow the existing slice pattern.** Entity (`BaseEntity`) → `AppDbContext` config → service behind an interface → hosted job following `CalendarSyncJob`/`OutboundStatusPollJob` → service-level xUnit tests against a fake Xero client → EF migration.
6. **No change to how jobs/invoices are created or edited.** This is purely additive — the Generate Invoice / Mark Paid / Reject flows keep working exactly as they do today if Xero is disconnected or misconfigured.

---

## 3. Domain model

### 3.1 `XeroConnection` — singleton row holding the live OAuth state

| Field | Type | Notes |
|---|---|---|
| `TenantId` | string | Xero organisation id (`xero-tenant-id`), returned at connect time |
| `TenantName` | string | display name shown on the settings page |
| `AccessToken` | string (encrypted) | short-lived (~30 min); refreshed proactively, never relied on past its expiry |
| `RefreshToken` | string (encrypted) | the durable credential — Xero rotates it on every refresh; 60-day sliding expiry if unused |
| `AccessTokenExpiresUtc` | DateTime | |
| `Scopes` | string | space-separated, as granted |
| `ConnectedAtUtc` / `ConnectedByUserId` | DateTime / Guid? | audit: who clicked "Connect" |
| `Status` | enum `XeroConnectionStatus` | `Connected` \| `Disconnected` \| `Error` (e.g. refresh failed, consent revoked) |
| `LastError?` | string | surfaced on the settings page |

Single row (same "singleton settings" shape as `BusinessDetails`/`CashFlowSettings`). Encryption via `IDataProtector` (`CreateProtector("XeroConnection")`) — never raw tokens in the DB, never logged.

### 3.2 `XeroSyncOutbox` — durable outbox, one row per pending push (app → Xero)

| Field | Type | Notes |
|---|---|---|
| `Action` | enum `XeroSyncAction` | `PushInvoice` \| `VoidInvoice` \| `PushPayment` |
| `InvoiceId` | Guid | **no FK** — must survive if the invoice row is ever hard-deleted (shouldn't happen per `Invoice.cs`'s own "never deleted" note, but matches the `CalendarSyncItem` precedent of not assuming) |
| `PartialPaymentId?` | Guid | set for `PushPayment` when pushing a specific partial payment; null when pushing the full Mark-Paid amount |
| `Attempts` / `NextAttemptUtc` / `LastError?` | int / DateTime / string? | identical backoff shape to `CalendarSyncItem` |

Coalescing: unique partial index on `(InvoiceId)` where `Action = 'PushInvoice'` — re-editing before the push runs just leaves the existing row, same as calendar sync. Rows deleted on success.

### 3.3 Fields added to existing entities

- **`Invoice`**: `XeroInvoiceId` (string?, Xero's own GUID — not our `Id`), `XeroSyncStatus` (enum: `NotSynced`/`Pending`/`Synced`/`Failed`/`Voided`), `XeroLastSyncedUtc` (DateTime?), `XeroLastError` (string?).
- **`Customer`**: `XeroContactId` (string?) — set the first time any of their invoices syncs successfully.

### 3.4 `XeroWebhookEvent` — inbound log (Xero → app)

Xero's webhook payload is an **"intent to notify"** — just `{eventCategory, eventType, resourceId, tenantId}`, no actual entity data. Store each raw event (dedupe by `(resourceId, eventType)` within the payload's timestamp window), ack within 5 seconds (Xero's hard requirement — the controller just enqueues and returns 200), then a background processor calls back into the Accounting API to fetch the real `Invoice`/`Payment` and apply it.

| Field | Type | Notes |
|---|---|---|
| `EventType` | string | e.g. `INVOICE.UPDATE` |
| `ResourceId` | string | the Xero-side entity id to fetch |
| `ReceivedAtUtc` | DateTime | |
| `ProcessedAtUtc?` | DateTime? | null = still pending |
| `ProcessingError?` | string | |

---

## 4. Phase 1 — One-way invoice push (Mobmek → Xero)

**Trigger:** `InvoiceService.GenerateDocumentAsync` (Invoice only, not Quotation — §1) and `RejectAsync`, in the same transaction as today's save, enqueue a `XeroSyncOutbox` row. No-op if `XeroConnection.Status != Connected`.

**Push job** (hosted, pattern: `CalendarSyncJob`), every ~1 minute, oldest-due-first, small batch (respecting the per-minute rate limit):

1. **Resolve Contact:** if `Customer.XeroContactId` is null, call Xero `Contacts` — search by email (fallback: name) — use the match if found, else `POST` a new Contact; store the id back on `Customer`.
2. **Map the invoice:**

   | Mobmek field | Xero `Invoices` field |
   |---|---|
   | `Type` | always `ACCREC` (sales invoice) |
   | `Contact.ContactID` | resolved Xero Contact id |
   | `InvoiceNumber` | our printed id (`INV-0001`) — keeps the two systems visually identical |
   | `Date` / `DueDate` | `CreatedAtUtc` (date part) / `DueDate` |
   | `LineItems[]` | one per `InvoiceItem`: `Description`, `Quantity`, `UnitAmount`, `TaxType` (mapped from `GstRate` — §4.1), `AccountCode` (a single configured default sales account code, §6 settings) |
   | `Status` | `AUTHORISED` on first push (so it's real/payable in Xero, not a draft) |
   | `Reference` | job title / `IssueName`, for a readable cross-reference |

3. `POST`/`PUT` (idempotent — `PUT` by `InvoiceID` once we have one); on success, store `XeroInvoiceId`, set `XeroSyncStatus = Synced`, `XeroLastSyncedUtc = now`; delete the outbox row.
4. **Reject → void:** `RejectAsync` enqueues `VoidInvoice`; the job sets the Xero invoice's `Status = VOIDED`. (Xero disallows voiding an invoice with payments already applied — if that happens, the job records the failure rather than silently skipping; the owner handles the edge case by hand in Xero. Expected to be rare since reject-while-partial-payments-exist is already blocked in-app per `invoices-quotes-public-booking.md`.)
5. Failure → `LastError` + same backoff ladder as calendar sync (1 min → 5 min → 30 min → hourly; error-level log past 24h).

### 4.1 GST/tax mapping

`Invoice.GstRate`/`TaxAmount` are a flat, already-snapshotted NZ GST figure (currently always 15%, tax-inclusive). Xero needs a `TaxType` code per line, not a rate — map our single GST rate to Xero's standard NZ `OUTPUT2` (15% GST on income) tax type for every line; this is a fixed lookup, not per-invoice logic, unless/until the business ever has zero-rated or exempt lines (not modeled today).

### 4.2 Quotations

**Explicitly deferred (§1).** Xero's "Quote" object is a separate API resource with its own numbering/status lifecycle; nothing here touches `DocumentType = "Quotation"` invoices.

---

## 5. Phase 2 — Payment status, two-way

This is the part that needs real care, because it changes an invariant: today `Invoice.IsPaid` is a boolean the app itself flips (`MarkPaidAsync`). Once Xero can also mark it paid, two independent writers exist for the same fact.

**Proposed resolution (flagged for explicit confirmation before building, not assumed):** stop treating "paid" as a boolean toggled by one action, and instead derive it from the *existing* `InvoicePartialPayment` list (already built, record-keeping only today — see `invoices-quotes-public-booking.md`) extended with a `Source` (`App`/`Xero`) and optional `XeroPaymentId`:

- `IsPaid` becomes **computed**: `Sum(payments) >= TotalAmount`, not a stored flag.
- A payment applied in **Mobmek** (Mark Paid / Partial Payment) inserts a `Source = App` row and enqueues `PushPayment`; the job calls Xero's `Payments` endpoint against the mirrored invoice.
- A payment applied in **Xero** (bank reconciliation) arrives via webhook/reconcile (below), and inserts a `Source = Xero` row directly — no further push, since Xero already has it.
- This avoids ever double-counting a payment that both sides somehow record, and reuses UI (`PartialPaymentHistoryList.tsx`) that already exists, just tagging each row with where it came from.

**Webhook receiver:** `POST /api/integrations/xero/webhook` — `[AllowAnonymous]`, verifies the `x-xero-signature` HMAC-SHA256 header against the raw body using the webhook signing key (separate secret from the OAuth client secret, generated on Xero's webhook subscription page), responds `200` within 5 seconds having only enqueued a `XeroWebhookEvent` row (no API calls inline). A background processor then fetches the real `Invoice`/`Payment` from Xero and applies it per the rule above.

**Reconcile pass (fallback for missed/failed webhooks):** hourly, using Xero's **Journals API** (a sequential, numbered feed of every accounting change — the pattern Xero itself recommends for "don't miss an update," stronger than polling each invoice individually) starting from the last processed journal number. Any journal line referencing a mirrored `Invoice.XeroInvoiceId` re-applies its current Xero-side payment state.

**Status endpoint:** `GET /api/integrations/xero/status` (admin) — connected y/n, tenant name, outbox depth, oldest pending, last webhook processed, last reconcile journal number, last error. Same shape as `GET api/calendarsync/status`.

---

## 6. Configuration & Xero-side setup (Phase 0)

One-time setup (owner + operator, no code — mirrors the Google Calendar doc's §7):

1. Create a free account at `developer.xero.com`, create an **app** (type: "Web app" for the standard OAuth2 flow).
2. Note the **Client ID** and **Client secret**; set the **redirect URI** to `https://<prod-domain>/api/integrations/xero/callback` (and a localhost one for dev).
3. Request scopes: `accounting.invoices`, `accounting.payments`, `accounting.contacts`, `accounting.settings.read` (to read the org's tax types/accounts for mapping), `offline_access` (required to receive a refresh token at all). **Corrected 2026-10-17, live-discovered**: Xero's old broad `accounting.transactions` scope no longer exists under the current granular-scope model — a connect attempt against it fails with `invalid_scope`. `accounting.invoices`/`accounting.payments` are its replacements. Each requested scope must also be explicitly ticked on the Xero app's Configuration → Scopes page before a consent attempt will succeed — it's not implied by picking an integration type.
4. Create a **webhook subscription** against the same app, pointed at `/api/integrations/xero/webhook`, subscribing to `Invoices` and `Payments`; note the generated webhook signing key.
5. In Mobmek: an admin-only **"Connect to Xero"** button (Settings) runs the OAuth2 Authorization Code flow, lands on the callback, stores the resulting tokens + tenant id into `XeroConnection`.
6. Pick (owner, in the Mobmek settings page) the **default Xero sales account code** line items post to (one dropdown, populated from Xero's `Accounts` endpoint via `accounting.settings.read`) — the one piece of Chart-of-Accounts mapping Phase 1 actually needs.

Config keys (non-secret): none required beyond the OAuth client id, which is not sensitive by itself. Secret-pattern identical to Resend/Google Calendar for the **client secret** and **webhook signing key** (env/user-secrets, never committed). The **refresh token is DB state, not config** — see §2.3 above.

**Token-refresh job:** a small hosted job refreshes the access token proactively (e.g. daily, independent of any sync activity) so the refresh token's 60-day sliding window never lapses purely from low invoice volume — a real risk for a single-mechanic shop that might not generate an invoice for days. On refresh failure (revoked consent, etc.), `XeroConnection.Status` flips to `Error` and the settings page surfaces it — the owner re-clicks "Connect to Xero" to re-consent.

**Custom Connection alternative (noted, not chosen):** if the owner's Xero plan is ever confirmed to support Custom Connections (machine-to-machine, no browser consent, no refresh-token babysitting), Phase 0 could be redone against that instead — simpler ongoing operation, same Phase 1/2 design above otherwise unaffected since it's purely an auth-layer swap behind `IXeroClient`.

---

## 7. Testing

Service-level xUnit against in-memory `AppDbContext` with a `FakeXeroClient` (records calls, scriptable responses/failures) — same shape as `FakeGoogleCalendarClient`:

- Enqueue: generate/reject each write the right outbox row in-transaction; unconfigured → no rows.
- Contact resolution: match-by-email, create-when-no-match, reuse-stored-id on subsequent invoices for the same customer.
- Push: correct line/tax/account mapping; idempotent re-push (`PUT` once an id exists); void blocked-by-payments surfaces as a tracked error, not silently dropped.
- Payment derivation: `IsPaid` computed correctly across mixed `App`/`Xero`-sourced payment rows; no double-count when both sides eventually agree on the same payment.
- Webhook: signature verification (valid/invalid/replayed), ack-fast-process-later ordering, dedupe.
- Reconcile: journal-number bookmark advances correctly; a missed webhook's effect still lands via the next reconcile pass.

---

## 8. Out of scope (v1)

- Quotations → Xero Quotes (§4.2).
- Full ledger mirror (expenses/bills, bank transactions, Chart-of-Accounts beyond the one sales account code) — §0's open strategic question.
- GST return filing through Xero — our GST Report stays review-only, unchanged.
- Credit notes/refunds.
- Multi-organisation / multi-currency.
- Settings UI beyond "Connect/Disconnect" + the one default account-code dropdown.

---

## 9. Rollout order

1. **Phase 0** — Xero app registration, OAuth connect flow, `XeroConnection`, token-refresh job, settings page.
2. **Phase 1** — Contact resolution + one-way invoice push/void, outbox + push job, Phase-1 tests.
3. **Decision checkpoint** — confirm the "derived `IsPaid`" data-model change in §5 before building it; it touches `MarkPaidAsync`/`InvoicePartialPayment`, both already shipped and tested.
4. **Phase 2** — Webhook receiver, Journals-API reconcile pass, payment push, status endpoint.
5. **Revisit §0's strategic question** — once Phase 2 has run for a while, decide explicitly whether to shelve the in-house bank-import/reconciliation phase of Cash Flow v2.
