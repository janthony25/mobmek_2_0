# Auth & Staff Management

**Last verified:** 2026-10-07 (read against `mobmek_api/src/MobmekApi/{Controllers,Services,Entities,Migrations}` and `mobmek_frontend/src/{pages,components/auth,contexts,api}`)

Covers login/session, account provisioning, roles & permissions (RBAC), employee records and their lookup tables, and self-service profile/password management.

`docs/auth-module-design.md` (dated 2026-07-08) is **stale** — it describes a simpler two-role MVP than what's actually shipped. Don't trust it without cross-checking here.

## Status summary

| Sub-capability | Status | Key file(s) |
|---|---|---|
| Login / logout / session | Working | `Controllers/AuthController.cs`, `contexts/AuthContext.tsx` |
| Login audit trail | Working | `Services/LoginAttemptService.cs` |
| Account provisioning (admin-invite + email confirm) | Working | `Services/AccountAdminService.cs`, `ConfirmAccountPage.tsx` |
| Account lifecycle (deactivate/reactivate/auto-purge) | Working | `Services/AccountAdminService.cs`, `Services/AccountPurgeJob.cs` |
| Password change (authenticated + forgot-password) | Working | `Controllers/{Account,Auth}Controller.cs`, `Services/AccountService.cs` |
| Roles & permissions (RBAC enforcement) | Working | `Services/Permissions.cs`, `Services/RoleService.cs`, `RequirePermission.tsx` |
| Employee CRUD | Working | `Services/EmployeeService.cs` |
| Employee titles / employment types (lookup tables) | Working | `Services/EmployeeTitleService.cs`, `Services/EmploymentTypeService.cs` |
| Profile settings (self-service) | Working | `Controllers/AccountController.cs`, `ProfileSettingsPage.tsx` |
| Data Protection keys / cookie infra | Working | `Program.cs:38-42` |

## Details

### Login / logout / session — Working
`AuthController.Login` (`Controllers/AuthController.cs:24-70`) uses `SignInManager.PasswordSignInAsync` with `lockoutOnFailure: true`; distinguishes `EmailNotConfirmed`/`LockedOut`(incl. deactivated)/`InvalidCredentials` without leaking account existence. Cookie `Mobmek.Auth`, HttpOnly, SameSite=Strict, 12h sliding (`Program.cs:64-88`). Frontend `AuthContext.tsx:26-31` bootstraps via `GET /api/auth/me`, has a global 401 handler.

**Fixed 2026-10-07:** added `tests/MobmekApi.Tests/Controllers/AuthControllerTests.cs` — constructs `AuthController` against a real `UserManager`/`SignInManager` (wired the same way as `Program.cs`, including the cookie auth scheme) rather than mocking them, so the branching is exercised for real. 6 tests: success, email-not-confirmed, deactivated, generic lockout, wrong password, unknown email (no existence leak).

### Login audit trail — Working
`LoginAttemptService.cs` persists every attempt (success/failure/reason/IP), paged endpoint gated `[Authorize(Policy = Permissions.ManageAccounts)]`. `IpAddress` is best-effort (shows proxy hop unless forwarded-header trust configured — flagged honestly in code).

### Account provisioning — Working
Admin-invite only, no self-registration. `AccountAdminService.CreateAsync` (`Services/AccountAdminService.cs:44-118`) validates role/employee/email, issues a hashed, 10-min-TTL confirmation token, emails an activation link, and **rolls back the account if the email send fails** so the employee's one-account slot isn't wasted. `ConfirmAccountAsync` sets the real password via Identity's `ResetPasswordAsync`, flips `EmailConfirmed`. Good test coverage (`AccountAdminServiceTests.cs`).

### Account lifecycle — Working (undocumented in design doc)
`DeactivateAsync`/`ReactivateAsync` (`Services/AccountAdminService.cs:161-212`): deactivation sets Identity lockout to `DateTimeOffset.MaxValue`, refuses to deactivate self or the last Admin. `AccountPurgeJob.cs` is an hourly `BackgroundService` that hard-deletes accounts deactivated 30+ days ago. Registered `Program.cs:176`.

### Password change — Working
`AccountController.RequestPasswordChangeCode`/`ConfirmPasswordChange` (`Controllers/AccountController.cs:36-70`) emails a 6-digit hashed code, 10-min TTL, fixed-time compare — requires an active session (self-service, for a user who knows their current password or is already logged in).

**Fixed 2026-10-07 — added the unauthenticated "forgot password" flow:** `AuthController.ForgotPassword`/`ResetPassword` (`Controllers/AuthController.cs`, both `[AllowAnonymous]`) reuse the exact same `PasswordChangeCode` mechanism (one shared `IssueCodeAsync` helper in `AccountService.cs`) but are keyed by email instead of a session. Deliberately anti-enumeration throughout: `RequestForgotPasswordCodeAsync` always returns `AccountError.None` whether the email is unknown, deactivated, unconfirmed, or genuinely sent — a code is silently *not* generated in the first three cases, and even a real send failure is swallowed rather than surfaced, so an anonymous caller can never distinguish "no such account" from "we sent it" by probing the endpoint. `ResetForgottenPasswordAsync` collapses "unknown email" and "wrong code" into the same generic `InvalidCode` response for the same reason. Deactivated accounts are excluded from both ends (checked again defensively in `ResetForgottenPasswordAsync`, not just at request time) — a deactivated account's password can't be reset this way, only an Admin reactivation unblocks it.

Frontend: `pages/ForgotPasswordPage.tsx` (new, route `/forgot-password`), linked from `LoginPage.tsx`'s new "Forgot password?" link. Two-stage form (request code → enter code + new password), mirroring `ProfileSettingsPage.tsx`'s existing password-change card UX. Live-verified in-browser (login → link → stage 1 submit → stage 2 renders → wrong-code submit shows the correct backend error message, no unexpected console errors).

10 new backend tests (`AccountServiceTests.cs`): unknown email, eligible user sends exactly one email, deactivated user, unconfirmed-email user, not-configured, correct-code reset, unknown-email reset, wrong code, expired code, second-request invalidates the first code.

### Roles & permissions (RBAC) — Working, genuinely enforced
Not just a schema — real ASP.NET Core policy-based auth. `Program.cs:90-110` sets a secure-by-default `FallbackPolicy = RequireAuthenticatedUser()` and one `AddPolicy` per permission in `Permissions.cs`'s fixed catalog (`ManageEmployees`, `ManageAccounts`, `ManageBusinessSettings`, `ManageCalendarSync`, `ManageReminderTemplates`, `AccessCashFlow`, `ViewJobMargins`). Roles are plain Identity rows, admin-creatable, with an admin-editable `RolePermission` mapping via `RolesController`/`RoleService.cs` — except the protected `Admin` role (always all permissions). Claims baked into the cookie at sign-in (`AppUserClaimsPrincipalFactory.cs`) — a permission change takes effect only on next login (deliberate, documented tradeoff). `JobRoleRedaction.cs` additionally redacts margin/cost fields for users lacking `ViewJobMargins`, independent of `[Authorize]`. Frontend `RequirePermission.tsx` gates routes 1:1 with backend policies (manually kept in sync — a maintenance risk, flagged in-code). `RoleServiceTests.cs` has solid coverage.

**This closes the RBAC gap noted in the industry-readiness analysis — at least at the HTTP layer.**

### Employee CRUD — Working
Full CRUD gated by `ManageEmployees`, validates `TitleId`/`EmploymentTypeId` exist. **Fixed 2026-10-07:** `EmployeeService.DeleteAsync` now returns `EmployeeWriteError.InUse` (mapped to a friendly 400) when the employee has a linked login account (`db.Users.AnyAsync(u => u.EmployeeId == id)`) or is assigned as a mechanic on a job (`db.JobMechanics.AnyAsync(...)`), instead of relying on the DB FK `Restrict` violation to surface as a raw 500. Covered by `EmployeeServiceTests.DeleteAsync_ReturnsInUse_WhenEmployeeHasLoginAccount`/`..._WhenEmployeeIsMechanicOnAJob`.

### Employee titles / employment types — Working
**Fixed 2026-10-07:** same pattern applied to both — `EmployeeTitleService.DeleteAsync`/`EmploymentTypeService.DeleteAsync` now check `db.Employees.AnyAsync(e => e.TitleId == id)` / `(e => e.EmploymentTypeId == id)` and return a new `InUse` error (mapped to a friendly 400) instead of letting the DB FK `Restrict` (`AppDbContext.cs:211-219`) turn into a 500. One new test each.

### Profile settings — Working
Self-service, deliberately scoped to name/contact fields only (title/employment-type/login-email stay Admin-managed). `ProfileSettingsPage.tsx` matches exactly, includes the password-change card.

### Data Protection keys / cookie infra — Working
`PersistKeysToFileSystem` to a mounted volume — session cookies survive container restarts. Cookie `SecurePolicy.SameAsRequest` resolves to `Secure` behind the TLS proxy.

**Correction (2026-10-07 follow-up check):** forwarded-header trust *is* correctly configured end-to-end, contradicting the earlier "unverified" note. Full chain, each hop confirmed by reading the actual config: Caddy terminates TLS and (by its default `reverse_proxy` behavior) sets `X-Forwarded-Proto: https` on its plain-HTTP hop to nginx → nginx (`mobmek_frontend/nginx.conf:1-11`) passes that header through via a `map $http_x_forwarded_proto $proxy_x_forwarded_proto` block (falling back to its own `$scheme` only when nothing was forwarded, i.e. local dev hit directly) → Kestrel's `app.UseForwardedHeaders(...)` (`Program.cs:339-345`) translates `X-Forwarded-Proto`/`X-Forwarded-For` into `Request.Scheme`/`RemoteIpAddress` before auth/cookie middleware runs → `CookieSecurePolicy.SameAsRequest` then correctly adds `Secure`. In-code comments on both the nginx map and the `Program.cs` block explicitly reference this as a bug that was already found and fixed. Not a gap.

## Design-doc drift (`docs/auth-module-design.md`)
1. Doc says "two roles only"; code has a full admin-editable role+permission system already.
2. Doc lists password reset as blocked on the email module; email has shipped, and an authenticated *change* flow was built — but true logged-out *recovery* still doesn't exist and isn't tracked as an open item anywhere.
3. Doc lists TLS as blocked on hosting choice; TLS is now live (`470632f`) and the forwarded-header-trust question (doc's open question #3) is confirmed resolved — see the cookie-infra section above.
4. Account lifecycle (deactivate/reactivate/purge) is fully built and unmentioned in the doc.
5. Doc references a `RequireAdmin` component; actual code has the more general `RequirePermission`.

## Recommended follow-ups
- ~~Add an "in use" pre-check to `EmployeeService`/`EmployeeTitleService`/`EmploymentTypeService` delete paths~~ — done 2026-10-07.
- ~~Add a controller-level test for `AuthController.Login`'s lockout-reason branching~~ — done 2026-10-07.
- ~~Decide and document whether a true logged-out "forgot password" flow is in scope~~ — built 2026-10-07 (self-service email reset, decided over admin-only reset).
