# Xero Integration

**Last verified:** 2026-10-17 — Phase 0 (connect/disconnect) live-verified against the real
"Mobile Mekaniko" Xero org; Phase 1 (invoice push) is code-complete and test-covered but **not
yet live-verified** (read against `Entities/{XeroConnection,XeroConnectionStatus,XeroSyncItem,
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
| Invoice push + contact resolution (Phase 1) | Code-complete, **not live-verified** | `Services/XeroSyncJob.cs`, `Services/InvoiceService.cs` |
| Default sales account-code setting | Code-complete, **not live-verified** | `XeroIntegrationController.cs`, `mobmek_frontend/src/pages/XeroSettingsPage.tsx` |
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

**Docker recovered and the stack was rebuilt (2026-10-17)**: `docker compose up -d --build`
applied the `AddXeroSyncPhase1` migration cleanly, the API started with no errors, and the Xero
connection survived intact (confirmed `Connected` to "Mobile Mekaniko" post-rebuild). All logic is
covered by 22 new unit tests (8 `XeroAccountingClientTests`, 8 `XeroSyncJobTests`, 6
`InvoiceServiceTests` enqueue cases) against fakes/stubs, full suite green (715/717 — the 2
failures are the pre-existing real-Postgres test).

**Still not live-verified against a real invoice**: `XeroConnection.DefaultSalesAccountCode` is
still unset (confirmed via DB read), which keeps `XeroSyncJob` cleanly paused by design (§2) — the
owner needs to pick a real Xero account code on the Xero settings page (requires their own login
session; not something this session could do) before generating a test invoice to confirm the
end-to-end push.

## Open tasks
- [ ] **Owner**: pick a default sales account code on the Xero settings page, then generate a real invoice and confirm it lands in Xero within ~1 minute — the one remaining step to call Phase 1 fully live-verified.
- [ ] Add the production redirect URI (`https://workshop.mobmekauto.co.nz/xero/callback`) to the Xero app and set `XERO_REDIRECT_URI` in the production `.env` before deploying this — currently only `localhost:3000` is registered.
- [ ] Phase 2 (two-way payment sync) — not started, see `docs/xero-integration-design.md` §5.
