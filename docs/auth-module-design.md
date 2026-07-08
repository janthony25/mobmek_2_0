# Authentication & Authorization — Design Document (v1)

**Status:** Phase 1 shipped 2026-07-08 (staff login, roles, route/endpoint guards). Phase 2 partially shipped same day: secrets moved to `.env`, Data Protection keys persisted, login audit trail added. Still open: TLS termination (blocked on hosting choice), password reset (blocked on the email module), 2FA. · **Date:** 2026-07-08
**Scope:** Staff login for the Mobmek workshop app (employees/admins), role-based authorization on the API, and secure session handling ahead of the first production deployment.

**Why now:** `Program.cs` currently calls `app.UseAuthorization()` with no `UseAuthentication()`, no identity store, and no `[Authorize]` attribute anywhere in the API — every controller (customers, invoices, cash flow, employees) is reachable by anyone who can reach the URL. That's fine on localhost; it is not fine once this is deployed to the public internet.

---

## 1. Current state

| Concern | Today |
|---|---|
| Authentication | None. No identity store, no login endpoint, no cookie/token issuance. |
| Authorization | None. `UseAuthorization()` is wired but there are zero `[Authorize]` attributes, so it's a no-op. |
| Transport | HTTP in Docker Compose; `UseHttpsRedirection()` is called but no cert/TLS termination is configured anywhere in the stack. |
| Frontend ↔ API | Same-origin in both dev (Vite proxy) and prod (nginx `location /api/` → `api:8080`), per `mobmek_frontend/nginx.conf` and `src/api/client.ts`. `client.ts` does not send `credentials: 'include'`. |
| Users | `Employee` (HR record: name, title, employment type, contact info) and `Customer` — neither has credentials, a login identifier, or a password. |
| Secrets | DB password (`postgres`/`postgres`) is hardcoded in `docker-compose.yml` and `appsettings.json`. |

---

## 2. Decisions (proposed — flag anything you want changed)

| Concern | Decision | Why |
|---|---|---|
| **Identity store** | **ASP.NET Core Identity**, backed by the existing PostgreSQL DB via a new `AppIdentityDbContext` (or folded into `AppDbContext`) | Built into the framework already in use (ASP.NET Core 10), gives password hashing (PBKDF2), lockout, role management, and password-reset tokens for free instead of hand-rolling them. |
| **Session transport** | **Cookie authentication** (`HttpOnly`, `Secure`, `SameSite=Strict`), not JWT-in-localStorage | Frontend and API are same-origin in every deployment shape this app has (nginx proxies `/api`). Cookies avoid the XSS-token-theft risk of storing bearer tokens in browser storage, and there's no second consumer (mobile app, third-party integration) that would need a portable token today. |
| **User ↔ Employee link** | Keep `Employee` as the HR record; add a separate Identity `ApplicationUser` with a required `EmployeeId` FK back to it | Keeps login credentials out of the HR table (which is exposed via `EmployeesController` DTOs today) and lets an employee exist before their account is provisioned. |
| **Roles (v1)** | `Admin`, `Employee` — two roles only | The app doesn't yet have a feature that needs finer-grained permissions than "can manage settings/financials" vs "day-to-day workshop use." `EmployeeTitle` (Manager/Mechanic/etc.) stays a pure HR field, not an auth role — don't conflate the two. Finer roles can be added later without a schema rewrite (Identity roles are just rows). |
| **Account provisioning** | Admin-created only, no public self-registration | This is an internal staff tool. An `Admin` creates an account (name + temp password or invite link) from an Employees screen; there's no `/register` page. |
| **Password policy** | Identity defaults, raised: min 10 chars, lockout after 5 failed attempts for 15 minutes | Balances real-world usability (workshop staff, not security engineers) against brute-force risk on a public endpoint. |
| **CSRF** | Rely on `SameSite=Strict` + custom header requirement (`fetch` already sends `Content-Type`/`Accept`, which simple cross-site form posts can't replicate) as the primary defense; add ASP.NET Core antiforgery tokens on state-changing requests if we later loosen `SameSite` | `SameSite=Strict` blocks the cookie from being sent on cross-site navigations/requests entirely, which covers the classic CSRF vector for an app with no cross-site embedding use case. |
| **Transport security** | Terminate TLS in front of the stack (reverse proxy / hosting provider cert, e.g. Caddy or the platform's managed HTTPS) before this goes live | Cookies marked `Secure` won't be sent over plain HTTP at all — auth silently breaks without TLS, so this has to land before deploy, not after. |
| **Secrets** | Move DB password and Identity signing keys out of `docker-compose.yml`/`appsettings.json` into environment variables (`.env`, not committed) or the host's secret manager | Currently `postgres`/`postgres` is committed in plaintext; not acceptable once internet-reachable. |
| **2FA** | Out of scope for v1, revisit for the `Admin` role once cash-flow/financial data is live | Adds real friction; ship password + lockout first, layer TOTP 2FA on `Admin` accounts once the basics are proven. |

---

## 3. Data model changes

```
ApplicationUser (new — Identity table, via IdentityUser<Guid>)
  Id, UserName (email), Email, PasswordHash, LockoutEnd, ...  ← Identity-managed
  EmployeeId (Guid, required, FK → Employee, unique)

Employee (unchanged) — HR record, no credential fields added
```

- One `ApplicationUser` per `Employee`; `EmployeesController` create/edit flows gain an "Invite / Create login" action rather than merging credentials into the HR entity.
- Identity's own tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc.) live in the same Postgres DB as everything else — one connection string, one set of migrations, no second datastore to operate.

---

## 4. Backend changes

- **Packages:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.Cookies` (the latter ships in the shared framework for ASP.NET Core apps).
- **`Program.cs`:**
  - `AddIdentityCore<ApplicationUser>()` + `AddRoles<IdentityRole<Guid>>()` + `AddEntityFrameworkStores<AppDbContext>()`.
  - `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)` — cookie name, `HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Strict`, sliding expiration (~8–12h to match a work shift).
  - Add `app.UseAuthentication()` before `app.UseAuthorization()` — this line is the actual bug fix; without it, `[Authorize]` attributes would 401 correctly but nothing currently issues or reads a session at all.
  - `AddAuthorization()` with two policies (or just role checks): `Admin`, `Employee`.
- **New `AuthController`:** `POST /api/auth/login` (email + password → sets cookie), `POST /api/auth/logout`, `GET /api/auth/me` (current user + role, for the frontend to bootstrap `AuthContext`).
- **Lock down existing controllers:** rather than an `[Authorize]` attribute on all 29 controllers individually, `AddAuthorization()` sets a `FallbackPolicy` of `RequireAuthenticatedUser()` — every endpoint requires a signed-in user by default, and any future controller is safe by default too. Only `AuthController.Login` opts out with `[AllowAnonymous]`. `[Authorize(Roles = "Admin")]` layers a role check on top for the 16 settings/financial/HR controllers: `EmployeesController`, `EmployeeTitlesController`, `EmploymentTypesController`, `BusinessDetailsController`, `GstSettingsController`, `GstReportController`, `CashFlowSettingsController`, `CashAccountsController`, `CashTransactionsController`, `CashFlowForecastController`, `CashFlowAuditController`, `RecurringTransactionsController`, `PlannedTransactionsController`, `PayeesController`, `CategorizationRulesController`, `TransactionCategoriesController`.
- **Seed:** `AdminSeeder` (mirroring the existing `CarReferenceDataSeeder` pattern), runs in every environment (not just Development, unlike the reference-data seeders — production needs the bootstrap account too), creates the first `Admin` account from `Bootstrap:AdminEmail` / `Bootstrap:AdminPassword` config (env vars `Bootstrap__AdminEmail` / `Bootstrap__AdminPassword` in production) if no Admin exists yet.
- **Login audit trail (Phase 2 item, shipped):** `LoginAttempt` entity + `LoginAttemptService`, recorded on every login attempt (success or failure, with reason) from `AuthController.Login`; `GET /api/auth/login-attempts` (Admin-only, paged) to view it. `IpAddress` is best-effort — behind the nginx reverse proxy it currently shows the proxy hop, not the real client, until forwarded-header trust is configured for the actual deployment target (see the TLS open question below — same underlying issue).
- **Data Protection key persistence (Phase 2 item, shipped):** auth cookies are signed/encrypted with the ASP.NET Core Data Protection key ring, which defaults to non-durable storage in a container. `PersistKeysToFileSystem` now points at a mounted volume (`mobmek_dataprotection_keys` in `docker-compose.yml`), confirmed by test: a session cookie issued before an `api` container restart is still valid after it.

## 5. Frontend changes

- `AuthContext` (React context + `useAuth()` hook) wrapping `App.tsx`, backed by `GET /api/auth/me` on load.
- `src/api/client.ts`: add `credentials: 'include'` to both `request()` and `apiPostForm()` — without this the browser won't send the session cookie on same-origin fetches from the SPA (it's same-origin, but `fetch` still defaults to `'same-origin'` only for credentials in older specs / omits in some configurations, so make it explicit).
- `LoginPage` (new, outside `AppLayout`, same pattern as `InvoicePrintPage` sitting outside the layout route).
- Route guard: a `RequireAuth` wrapper route element redirecting to `/login` when `useAuth()` has no user; an admin-only variant for settings routes (`business-details`, `tax-settings`, `employees`, `cash-*`).
- Global 401 handling in `client.ts`: on a 401 response, clear local auth state and redirect to `/login` (covers session expiry mid-use).

## 6. Rollout phases

**Phase 1 — Core staff auth** ✅ shipped 2026-07-08
- [x] Identity packages + migration (`AspNetUsers`/`AspNetRoles` tables)
- [x] `ApplicationUser` ↔ `Employee` link, `Admin`/`Employee` roles
- [x] Cookie auth wired in `Program.cs`, `AuthController` (login/logout/me)
- [x] Fallback-policy authorization (every endpoint requires login by default) + role checks on 16 settings/financial/HR controllers
- [x] Seed first Admin account from env config (`AdminSeeder`, runs in every environment)
- [x] Frontend: `AuthContext`, `LoginPage`, route guards (`RequireAuth`/`RequireAdmin`), `credentials: 'include'`, 401 redirect, sidebar sign-out + admin-only nav filtering

**Phase 2 — Hardening before/at deploy**
- [ ] TLS termination in front of the stack; confirm `Secure` cookies actually work end-to-end — **blocked on hosting choice**, see open question below
- [x] Move DB password + bootstrap-admin credentials to `.env` (gitignored, `.env.example` checked in) instead of committed `docker-compose.yml`/`appsettings.json`
- [x] Persist Data Protection keys to a volume so container restarts don't invalidate every session
- [ ] Password reset flow (reuses `IEmailSender` from the email module, see `docs/email-module-design.md`) — **blocked: email module is not built yet**
- [x] Login audit trail (who logged in, when, failed attempts) — `LoginAttempt` entity/service, `GET /api/auth/login-attempts` (Admin-only)
- [ ] Optional: TOTP 2FA for `Admin` accounts

---

## 7. Open questions for you

1. ~~Two roles (`Admin`/`Employee`) enough for v1?~~ **Resolved:** yes, `Admin`/`Employee`.
2. ~~Bootstrap the first Admin from env vars, or a setup wizard?~~ **Resolved:** env-var seeded (`AdminSeeder`).
3. **Still open:** any target host/TLS setup picked for the "deploying soon" plan (VPS + Caddy, a PaaS with managed HTTPS, etc.)? This decides how the TLS item gets implemented, and also how `LoginAttempt.IpAddress` and the `Secure` cookie flag get real forwarded-header trust configured (both currently see the nginx proxy hop, not TLS/the real client, since that config depends on knowing the actual proxy topology).
