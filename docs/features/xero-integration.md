# Xero Integration

**Last verified:** 2026-10-17 (new area — read against `Entities/XeroConnection{,Status}.cs`,
`Services/{IXeroAuthService,XeroAuthService,XeroTokenRefreshJob}.cs`,
`Controllers/XeroIntegrationController.cs`, migration `AddXeroConnection`, 13 new
`XeroAuthServiceTests`, `mobmek_frontend/src/pages/{XeroSettingsPage,XeroCallbackPage}.tsx`)

Full plan: `docs/xero-integration-design.md` / `-todo.md`. This file tracks only what's actually
built against that plan — most of it is still "Planned only."

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| OAuth2 connect/disconnect (Phase 0) | Working, not yet live-verified against a real Xero org | `Services/XeroAuthService.cs`, `Controllers/XeroIntegrationController.cs` |
| Encrypted token storage | Working | `XeroConnection.cs` (Data-Protection-encrypted), `XeroAuthService.cs` |
| Daily proactive token refresh | Working | `Services/XeroTokenRefreshJob.cs` |
| Connect/Disconnect settings page | Working, not yet live-verified | `mobmek_frontend/src/pages/XeroSettingsPage.tsx` |
| Invoice push to Xero (Phase 1) | **Planned only** | — |
| Two-way payment/paid-status sync (Phase 2) | **Planned only** | — |

## Details

### OAuth2 connect/disconnect — built, not yet live-verified
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

**Not yet live-verified**: this requires a real Xero developer app (Client ID/secret, registered
redirect URI) and a real browser OAuth consent — both need the account owner, see the open task
below. Nothing in this repo can self-test an actual Xero login.

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
- [ ] **Owner**: register a Xero developer app at `developer.xero.com`, set `XERO_CLIENT_ID`/`XERO_CLIENT_SECRET`/`XERO_REDIRECT_URI` in `.env`, then click "Connect to Xero" and confirm a real OAuth consent completes (`docs/xero-integration-todo.md` Phase 0).
- [ ] Live-verify the daily refresh job actually rotates tokens against a real connection once one exists.
- [ ] Phase 1 (invoice push) and Phase 2 (payment sync) — not started, see `docs/xero-integration-design.md`.
