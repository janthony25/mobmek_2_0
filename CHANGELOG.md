# Changelog

A running, human-readable log of what shipped and why — reverse-chronological, grouped by
date. One entry per commit, added in the same commit that makes the change. See the rule in
the root `CLAUDE.md` ("Rule: log every commit in `CHANGELOG.md`") for how this is kept current.

## 2026-10-07

- **Guard irreversible deletes behind `ConfirmDialog`; fix four mobile/touch nav bugs** — job-photo, business-logo, and cash-transaction-attachment "Remove" buttons no longer delete instantly on click (new app-wide rule in `mobmek_frontend/CLAUDE.md`, found via a user report on job photos then audited into two more instances). Separately, fixed four bugs reported live from a phone against production: the mobile shell used `h-screen` instead of `h-dvh`, so the top bar felt "stuck" behind Safari's collapsing address bar; dropdown/combobox menus (`DropdownMenu`, `Combobox`, `AsyncCombobox`, the customer invoice date filter) could stay stuck open on iOS since they only listened for `mousedown`, not `touchstart`; the `CrudSection` list-page header didn't wrap on narrow screens; and the desktop sidebar's icon-only collapse preference was leaking into the mobile/tablet drawer, hiding all nav labels. None of the four nav fixes were re-verified on a real device this session (no browser automation available) — worth a real-phone/tablet pass.
- **Add `CHANGELOG.md` and the rule to keep it updated every commit** — backfilled with every commit from 2026-10-06 onward; the `CLAUDE.md` rule requires a new entry in the same commit as any future change.
- **Redesign sidebar to minimalist light theme, add tablet/mobile nav** — light theme with `lucide-react` icons and a rounded-pill active state; sidebar and notes panel become off-canvas drawers below 1024px. (`b1c018b`)
- **Add Resend webhook, editable email templates, and reminder/appointment email** — Svix-verified delivery webhook, an `EmailTemplate` system driving invoice wording, and new email actions on reminders/appointments. (`3b3e551`)
- **Make invoice/quote prefixes configurable and convert-on-arrival transactional** — configurable INV/QUO prefixes in Business Settings; appointment-to-job conversion is now atomic instead of a separate post-hoc update that could silently fail. (`617d6d4`)
- **Close out the 2026-10-07 feature-gap audit fixes** — forgot-password flow, delete guards on employees/titles/employment types, customer/vehicle duplicate detection, a customer notes panel, concurrency-safe invoice numbers, a unified QuestPDF invoice/quote path, and the `docs/features/` living status-doc system. (`cea6459`)
- **Add `./scripts/publish.sh` to wrap the manual EC2 deploy over SSH** — prevents deploying from an uncommitted/unpushed tree or forgetting `--build`; migrations stay a separate, reviewed-by-hand step. (`798e490`)
- **Wire up Send Email on the Customer page and global invoice/quote lists** — the compose-modal flow already working on Job Detail is now reachable from the other two places customers expected it. (`090c144`)
- **Backups are live: nightly cron running, restore drill passed for real** — confirmed on the real box: cron active, a real backup uploaded, and a real restore drill (48 tables, 1 account) against a disposable Postgres. Closes out the Phase 1 checklist. (`90e529d`)
- **Add nightly S3 backup and a real restore drill** — `backup-to-s3.sh` (timestamped, never-overwritten dumps) and `restore-drill.sh` (restores into a scratch container and sanity-checks table/account counts). (`61caa03`)
- **TLS is live: https://workshop.mobmekauto.co.nz** — real Let's Encrypt cert via Caddy; the Secure cookie flag confirmed active over the real domain. (`470632f`)
- **Add Caddy for automatic TLS** — new optional `tls` compose profile; also fixed `X-Forwarded-Proto` getting hardcoded to nginx's own scheme on the Caddy→nginx hop, which would have silently defeated the Secure-cookie fix one hop further in. (`0b985b6`)
- **Deploy the app to the EC2 instance — live at http://3.102.246.171** — first live deploy; fixed a missing-migration crash (production correctly doesn't auto-migrate) and the frontend container being unreachable on the wrong host port. (`e10ab30`)
- **Launch the Phase 1 EC2 instance and Elastic IP** — `t4g.small` Ubuntu 24.04 arm64 instance provisioned; infrastructure only, nothing deployed yet. (`8e6ae38`)

## 2026-10-06

- **Create SSM parameters, fix stale SSH rule and missing KMS decrypt grant** — refreshed the stale admin-IP SSH rule, seeded all 6 app secrets into SSM, and added the `kms:Decrypt` grant actually needed to read them. (`22e36dc`)
- **Split "can view baseline data" from "can manage it" on two controllers** — fixed non-Admins being silently blocked from the mechanic picker and the invoice letterhead; opened safe read paths without loosening the genuinely sensitive write permissions. (`cd9cf61`)
- **Fix `ResendConfigured` lying when `FromAddress` was never actually set** — account creation was failing with a misleading "try again in a moment" message; the configured check now actually requires a usable From address, not just an API key. (`33b7f9b`)
- **Make Roles & Permissions read-only with an explicit Edit action** — stops every checkbox click from saving immediately; changes now go through an explicit Edit → Save/Cancel per role. (`4f4b946`)
- **Make the frontend permission-aware instead of hardcoded to "Admin"** — sidebar and route guards now check real permissions via `/auth/me`, not the literal "Admin" role name. (`e1f4e9d`)
- **Add a Roles & Permissions UI on top of the permission-claim plumbing** — admin-facing surface for creating roles and toggling permissions, replacing direct writes to the grant table. (`96d3cd7`)
- **Replace hardcoded Admin role checks with a permission-claim system** — ~20 controllers moved off compile-time `Roles = "Admin"` onto an admin-editable permission catalog — the RBAC groundwork. (`4ad3442`)
- **Hide cost/margin fields from non-Admin callers on jobs and job items** — profit/markup/trade-price fields now null out for non-Admin accounts instead of going to every authenticated caller. (`5701b60`)
- **Fix auth cookie never getting Secure behind the nginx/Caddy proxy** — `X-Forwarded-Proto` wasn't being translated into `Request.Scheme`, so the Secure cookie flag could never activate behind TLS termination. (`f525008`)
- **Add a way to run EF Core migrations in production** — `generate-migration-script.sh` produces an idempotent SQL script applied via the Postgres image's own `psql`, since the deployed API image has no SDK/`dotnet-ef`. (`15ff8fc`)
- **Raise job photo upload limit to 25 MB, fix nginx silently capping it at 1 MB** — nginx had no `client_max_body_size` set, so it was rejecting large uploads before the app's own (too-low) limit even applied. (`ce72892`)
- **Switch uploaded file storage from local disk to S3** — job photos/receipts/logo were being written to the API container's own filesystem, which a rebuild discards; now S3-backed with a startup guard against misconfiguration. (`d7b60e7`)
