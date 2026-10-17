# Xero Integration

**Last verified:** 2026-10-17 — live-verified against the real "Mobile Mekaniko" Xero org (read
against `Entities/XeroConnection{,Status}.cs`, `Services/{IXeroAuthService,XeroAuthService,
XeroTokenRefreshJob}.cs`, `Controllers/XeroIntegrationController.cs`, migration
`AddXeroConnection`, 13 `XeroAuthServiceTests`,
`mobmek_frontend/src/pages/{XeroSettingsPage,XeroCallbackPage}.tsx`, and a direct
`SELECT * FROM "XeroConnections"` confirming `Status = Connected`)

Full plan: `docs/xero-integration-design.md` / `-todo.md`. This file tracks only what's actually
built against that plan — most of it is still "Planned only."

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| OAuth2 connect/disconnect (Phase 0) | **Working, live-verified** | `Services/XeroAuthService.cs`, `Controllers/XeroIntegrationController.cs` |
| Encrypted token storage | Working | `XeroConnection.cs` (Data-Protection-encrypted), `XeroAuthService.cs` |
| Daily proactive token refresh | Working, not yet live-verified (needs 24h+ to observe a real rotation) | `Services/XeroTokenRefreshJob.cs` |
| Connect/Disconnect settings page | **Working, live-verified** | `mobmek_frontend/src/pages/XeroSettingsPage.tsx` |
| Invoice push to Xero (Phase 1) | **Planned only** | — |
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

## Open tasks
- [ ] Live-verify the daily refresh job actually rotates tokens against the real connection (needs 24h+ of the container staying up, or a manual trigger).
- [ ] Add the production redirect URI (`https://workshop.mobmekauto.co.nz/xero/callback`) to the Xero app and set `XERO_REDIRECT_URI` in the production `.env` before deploying this — currently only `localhost:3000` is registered.
- [ ] Phase 1 (invoice push) and Phase 2 (payment sync) — not started, see `docs/xero-integration-design.md`.
