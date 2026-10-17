# Xero Integration

**Last verified:** 2026-10-17 — Phase 0 (connect/disconnect) and Phase 1 (invoice push) **both
live-verified** against the real "Mobile Mekaniko" Xero org, including a real pushed test invoice
(INV-0671) (read against `Entities/{XeroConnection,XeroConnectionStatus,XeroSyncItem,
XeroSyncAction,XeroInvoiceSyncStatus}.cs`, `Services/{IXeroAuthService,XeroAuthService,
XeroTokenRefreshJob,IXeroAccountingClient,XeroAccountingClient,XeroSyncJob}.cs`,
`Controllers/XeroIntegrationController.cs`, `Services/InvoiceService.cs`'s `EnqueueXeroPush`/
`EnqueueXeroVoid`, migrations `AddXeroConnection`/`AddXeroSyncPhase1`, 13 `XeroAuthServiceTests` +
8 `XeroAccountingClientTests` + 8 `XeroSyncJobTests` + 6 `InvoiceServiceTests` enqueue cases,
`mobmek_frontend/src/pages/{XeroSettingsPage,XeroCallbackPage}.tsx`)

Full plan: `docs/xero-integration-design.md` / `-todo.md`. This file tracks only what's actually
built against that plan — most of it is still "Planned only."

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| OAuth2 connect/disconnect (Phase 0) | **Working, live-verified** | `Services/XeroAuthService.cs`, `Controllers/XeroIntegrationController.cs` |
| Encrypted token storage | Working | `XeroConnection.cs` (Data-Protection-encrypted), `XeroAuthService.cs` |
| Daily proactive token refresh | **Working, live-verified** (one real rotation observed on API restart 2026-10-17) | `Services/XeroTokenRefreshJob.cs` |
| Connect/Disconnect settings page | **Working, live-verified** | `mobmek_frontend/src/pages/XeroSettingsPage.tsx` |
| Invoice push + contact resolution (Phase 1) | **Working, live-verified** | `Services/XeroSyncJob.cs`, `Services/InvoiceService.cs` |
| Default sales account-code setting | **Working, live-verified** | `XeroIntegrationController.cs`, `mobmek_frontend/src/pages/XeroSettingsPage.tsx` |
| Two-way payment/paid-status sync (Phase 2) | **Planned only** | — |

## Details

### OAuth2 connect/disconnect — built and live-verified
`XeroAuthService` is a hand-rolled client (no third-party SDK — `docs/xero-integration-design.md`
§1) against `login.xero.com`/`identity.xero.com`/`api.xero.com`. `BuildAuthorizationUrlAsync`
generates a one-time state nonce (10-minute TTL) stored on the singleton `XeroConnection` row;
`CompleteConnectionAsync` validates it, exchanges the code, fetches the connected tenant, and
stores everything. `DisconnectAsync` clears tokens locally (does not call Xero to revoke consent
— the owner can do that from Xero's own connected-apps page). Gated end-to-end on
`Permissions.ManageIntegrations` (`XeroIntegrationController`). 13 tests cover state
validation/expiry/one-time-use, token exchange, refresh success/failure, and the
near-expiry-triggers-refresh path in `GetValidAccessTokenAsync` — all against a stubbed
`HttpMessageHandler`, no real Xero credentials needed for tests.

**Live-verified 2026-10-17**: connected to the real "Mobile Mekaniko" Xero org via a real browser
OAuth consent, all 5 requested scopes granted — confirmed by a direct DB read of the
`XeroConnections` row (`Status = Connected`).

**Bug found and fixed same day**: the original scope list (`accounting.transactions` among
others) 500'd with `invalid_scope` on the first live connect attempt — Xero's old broad
`accounting.transactions` scope no longer exists under their current granular-scope model (the
app's own Configuration page scope list, fetched live, confirms it's simply absent). Corrected to
`accounting.invoices`/`accounting.payments`, its replacements, rebuilt, and reconnected
successfully. `docs/xero-integration-design.md` §6 updated to match.

### Why the callback is a frontend route, not a backend endpoint
The auth cookie is `SameSite=Strict` (`Program.cs`) — it would be dropped on the cross-site
redirect arriving directly from `login.xero.com`. The registered `Xero:RedirectUri` points at
`XeroCallbackPage.tsx` (a frontend route); once the browser has landed back on our own origin,
that page's `fetch` to `POST .../complete` is same-site and carries the cookie correctly.

### Token encryption
`XeroConnection.AccessToken`/`RefreshToken` are encrypted via `IDataProtectionProvider` before
ever reaching Postgres (`CreateProtector("XeroConnection")`) — reuses the same Data Protection key
ring already persisted to a Docker volume for the auth cookie (`docker-compose.yml`:
`mobmek_dataprotection_keys:/keys`), so no new persistence setup was needed.

### Invoice push (Phase 1) — code-complete, not live-verified
`InvoiceService.GenerateAsync`/`AcceptQuotationAsync` enqueue a `XeroSyncItem(PushInvoice)` row
(skipped for Quotations); `RejectAsync` enqueues `VoidInvoice`. Both no-op when
`IXeroAuthService.IsConfigured` is false — same shape as the calendar sync's `AppointmentService`
hooks. `XeroSyncJob` (hosted, 1-minute tick) resolves/creates a Xero Contact by email (falls back
to create-without-email when the customer has none), reuses `Customer.XeroContactId` on every
later invoice, maps lines tax-inclusive with a fixed `OUTPUT2` GST tax type and the configured
default account code, and pushes via `IXeroAccountingClient` (hand-rolled `HttpClient`, no SDK —
same approach as `XeroAuthService`). Also no-ops until a default sales account code is set on the
Xero settings page. Failure records the error on both the outbox row (for backoff: 1 min → 5 min →
30 min → hourly) and the invoice itself (`XeroSyncStatus`/`XeroLastError`).

**Live-verified end to end, 2026-10-17** — both directions:
- **Push**: owner picked account `200` (Sales) on the settings page, generated a real test
  invoice (INV-0671, $11.00), and it pushed to Xero successfully — `XeroSyncStatus = Synced`, a
  real `XeroInvoiceId` stored.
- **Void**: rejecting the same invoice in Mobmek enqueued a `VoidInvoice` row, processed on the
  next tick, confirmed `XeroSyncStatus = Voided` with no error. The test invoice is now cleanly
  `VOIDED` in the real Xero org rather than a dangling fake `AUTHORISED` one — safe to leave
  permanently, no further cleanup needed before production.

**Bug found and fixed in the process**: the first live attempt 400'd with `"The document DueDate
field must be specified"` — Xero requires a `DueDate` on every `AUTHORISED` invoice, but Mobmek
lets an invoice be generated with no due date at all (`Invoice.DueDate` is nullable), and the
push mapping was forwarding `null` straight through. Fixed in `XeroSyncJob.ProcessPushAsync`:
defaults to the invoice's issue date ("due on receipt") when Mobmek has no due date set, rather
than inventing a payment-term length — never written back onto `Invoice.DueDate` itself, only
what's sent to Xero. New regression test (`XeroSyncJobTests.PushInvoice_NoDueDateSet_
DefaultsToIssueDate_NotLeftNull`). Rebuilt, forced the pending outbox row to retry immediately
(bypassing the backoff wait), confirmed it synced successfully.

### Discounted invoices overstated the Xero total — fixed (2026-10-17)
Mobmek subtracts `Invoice.Discount` before adding GST (`InvoiceService.cs`), but the Xero push
mapping sent only the raw, undiscounted `InvoiceItem.ItemPrice` lines with no corresponding
reduction — so a discounted invoice would've matched in Mobmek's own PDF but overstated the
total in Xero by the discount amount. Caught while reviewing the calculation pipeline after the
`LineAmountTypes` fix above, before any real discounted invoice hit this. Fixed in
`XeroSyncJob.BuildLines`: when `Invoice.Discount > 0`, an extra negative line ("Discount",
`-Discount`, same tax type) is appended — the standard way to represent a discount in Xero's API
(no invoice-level discount field exists) — which lands on exactly the same total Mobmek computes,
since a negative line reduces Xero's own computed GST proportionally too.

### GST understated every pushed invoice's Xero total — fixed (2026-10-17)
The push code set `LineAmountTypes: "Inclusive"`, telling Xero the line prices already included
GST — but `InvoiceService.GenerateDocumentAsync` actually adds GST *on top* of the subtotal
(`"GST is added on top of the discounted subtotal"`, confirmed in-code). Result: for INV-0672
($300 subtotal + $45 GST = $345 in Mobmek), Xero showed Total = $300.00 — GST computed as an
internal breakdown of that figure rather than added on top, silently understating revenue in
Xero by the GST amount on every invoice pushed so far. Caught by comparing the Mobmek-generated
PDF against the pushed Xero invoice side by side. Fixed: `LineAmountTypes: "Exclusive"`.
**INV-0672 in Xero still reflects the wrong $300.00 total** — not yet re-pushed with the fix (see
open tasks).

### Singleton connection row — fixed a real race condition (2026-10-17)
`XeroAuthService.GetOrCreateConnectionAsync` used to be a plain "select the first row, insert one
if none exists" — fine under a single caller, but `XeroTokenRefreshJob` ticks immediately on
every app startup and can race a user's own "Connect" click at exactly that moment (confirmed
live on the first production connect attempt, right after a restart): both saw zero rows, both
inserted, producing two. The pending OAuth state written by Connect landed on one row; Complete
read from the other; 400'd as "expired or invalid." Fixed by keying the row to a fixed, known
`Guid` and catching the resulting Postgres unique-violation as a normal race outcome (re-read the
winner's row) rather than ending up with two — same retry-on-conflict shape `InvoiceService`
already uses for sequence numbers. Pre-existing duplicate/orphaned rows cleared on both local and
production as part of the fix.

**Live in production, 2026-10-17**: merged to `main`, migration applied to the production
database (backed up first), deployed, and connected for real — `Connected` to "Mobile Mekaniko",
default sales account `200`, single clean connection row. Every real invoice generated from here
on pushes to the live Xero org; rejecting one voids it there too.

## Open tasks
- [ ] **Re-push INV-0672 in Xero with the corrected `Exclusive` line amounts** — it currently shows the wrong $300.00 total (should be $345.00) from before the GST fix. Needs a fresh `XeroSyncItem(PushInvoice)` row enqueued manually (reusing its existing `XeroInvoiceId`) since it's already `Synced` and won't re-enqueue on its own.
- [ ] Decide when to test a real (non-test) invoice push/reject on production, if not already implicitly covered by normal day-to-day use from here.
- [ ] Add the production redirect URI (`https://workshop.mobmekauto.co.nz/xero/callback`) to the Xero app and set `XERO_REDIRECT_URI` in the production `.env` before deploying this — currently only `localhost:3000` is registered.
- [x] ~~Delete/void the real "INV-0671" test invoice~~ (done 2026-10-17, as a side effect of live-testing the void path) — rejecting it in Mobmek voided it in Xero for real. Cleanly `VOIDED`, safe to leave permanently.
- [ ] Phase 2 (two-way payment sync) — not started, see `docs/xero-integration-design.md` §5.
