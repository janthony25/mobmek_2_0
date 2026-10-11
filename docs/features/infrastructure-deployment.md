# Infrastructure & Deployment

**Last verified:** 2026-10-11 (added `.github/workflows/ci.yml` and the migration-drift check in `deploy.sh` this session; verified both against EC2/synthetic data, not just reasoned about)

## Status summary

| Sub-capability | Status |
|---|---|
| TLS | Working, live |
| CI (build + test gate) | **Working, push-based** — see below |
| Deploy mechanism | Manual scripted SSH, no auto-deploy (deliberate) — now with a migration-drift check, see below |
| Nightly backup | Working |
| Restore drill | Working, genuinely tested |
| Docker Compose service topology | Working |
| Monitoring / alerting / error tracking | **Missing entirely** |

## Details

### TLS — Working
`Caddyfile:6-8`: `workshop.mobmekauto.co.nz { reverse_proxy frontend:80 }`, automatic Let's Encrypt via the `caddy` compose service (profile `tls`, not started by default compose). Commit `470632f` states DNS propagated and a real cert was obtained with a verified `Secure` `Set-Cookie` header over the real domain — self-reported via commit message and `infrastructure/README.md`'s checklist, not independently re-verified live in this audit (no live network check performed).

### CI (build + test gate) — Working, push-based, added 2026-10-11
`.github/workflows/ci.yml`: two parallel jobs on every push to `main` (plus manual `workflow_dispatch`) — `backend` (`dotnet build`, `dotnet tool restore`, `dotnet dotnet-ef database update` against a service-container Postgres 17 on port 5433, then `dotnet test`) and `frontend` (`npm ci`, `npm run build`, `npm run lint`). Deliberately **push-based, not PR-gated**: this repo has always pushed straight to `main` (confirmed via `git branch -a` — no branches, no PRs, ever), so this runs as a post-push signal to check before running `deploy.sh`, not a merge-blocking gate — switching to PR-based + branch protection is a real option later if the ceremony becomes worth it. Verified locally before committing by swapping the real dev DB for a genuinely empty `postgres:17` container on the same port and running the exact same step sequence — all 647 tests passed against a from-scratch schema, not just the already-migrated dev DB.

The Postgres service container matters specifically because of `InvoiceSequenceIndexTests.cs` (see `legacy-data-import.md`) — the first test in the suite needing a real Postgres, since EF Core's InMemory provider (used by the other 645) ignores partial/filtered unique indexes entirely.

### Deploy mechanism — manual scripted SSH, no auto-deploy
`scripts/deploy.sh` SSHs to a hardcoded EC2 host, requires a clean/pushed `main`, runs `git pull && docker compose up -d --build api frontend` remotely, curls the domain for a 200. Explicitly does **not** touch the DB/migrations (that's a separate step via `scripts/generate-migration-script.sh` + `scripts/apply-migration.sh`). This is a deliberate choice, not an oversight: a single EC2 box with effectively one deployer doesn't get much from fully automating the deploy step itself, and the manual script already prevents deploying an uncommitted/unpushed tree. The actual decision was CI (build+test, above) **without** wiring it to auto-deploy.

**A real production outage happened here, 2026-10-07**, from a gap CI would *not* have caught: two commits shipped the same day each included an EF migration (`AddInvoiceSequenceNumberUniqueIndex`, `AddInvoiceQuotePrefixToBusinessDetails`, `AddEmailTemplate`, `AddReminderAndAppointmentEmailLinks`). Code was deployed via `deploy.sh` as normal; the separate manual migration step was forgotten. The deployed code then read columns (`BusinessDetails.InvoicePrefix`/`QuotePrefix`) that didn't exist in the live DB, so every "Generate Invoice"/"Generate Quotation" click on the live site threw a generic 500 — caught only because the user happened to hit it on their phone and reported it. Fixed in the same session by running the documented `generate-migration-script.sh` → `pg_dump` backup → `psql` apply procedure; confirmed via `__EFMigrationsHistory`. See `invoices-quotes-public-booking.md` for the full incident detail.

**Fixed 2026-10-11 — `deploy.sh` now checks for this before every deploy.** After `git pull` on the box, it diffs every migration file in the pulled commit's `Migrations/` folder against what the box's own `__EFMigrationsHistory` table actually has (via the same SSH session — `docker compose exec -T db psql`, no new tooling on the box), and fails loudly *before* rebuilding containers if anything's missing. Verified both directions before relying on it: ran for real against EC2 (correctly found zero pending, since that migration was already applied earlier this session) and against synthetic data with a deliberately-missing entry (correctly flagged it via `comm -23`).

**Fixed 2026-10-12 — the pre-migration backup is no longer just a documented recommendation.** `scripts/apply-migration.sh` is new: it copies `migrate.sql` to the box, takes a fresh `pg_dump` (aborting if it looks suspiciously small — the same size-sanity pattern `backup-to-s3.sh` already used), applies the migration with `ON_ERROR_STOP=1`, verifies the 5 most recent rows in `__EFMigrationsHistory`, then cleans up the gitignored `migrate.sql` on both ends. Previously this was a fully manual `scp`/`ssh`/`pg_dump`/`psql` sequence documented in `infrastructure/docs/phase-1-plan.md` — the backup step existed only as a comment telling the operator to remember it.

### EC2-to-local data sync — Working, added 2026-10-11
`scripts/sync-ec2-to-local.sh`: pulls a full `pg_dump` of EC2's production database down and restores it into local Postgres — schema, data, users, settings, everything — so manual testing happens against real production-shaped data before publishing, rather than on EC2 itself. Backs up local's current state first (`/tmp/mobmek-local-before-sync-*.sql.gz`) as a quick safety net, stops the local `api` container to release connections, terminates any remaining backends, drops/recreates the local `mobmek` database, restores, restarts. Verified for real: row counts matched exactly between local and EC2 after running it (487 customers/529 cars/759 jobs/864 invoices/1 user, both sides). **Known consequence:** the local admin login becomes the real EC2 admin account (same email, real production password) — the dev-default `DevAdmin!2026` password stops working against this data until a fresh local DB is re-migrated/seeded.

### Nightly backup — Working
`scripts/backup-to-s3.sh`: `pg_dump` via `docker compose exec`, gzip, size sanity check, uploads to S3 via instance IAM role (no credentials in the script). Matches `infrastructure/README.md` (daily cron at 13:00 UTC).

### Restore drill — Working, genuinely tested
`scripts/restore-drill.sh` downloads the most recent (or a specified) backup, restores into a **disposable scratch Postgres container** (never the real `db` service), sanity-checks table count and reads `AspNetUsers` count, fails loudly on any step. This is a real drill, not a stub.

### Docker Compose topology — Working
Services: `db` (postgres:17, host port 5433), `api` (ASP.NET, S3/local file-storage toggle, Resend/GCal env vars wired but empty by default), `legacy-mssql` (profile `legacy`, still present pending Phase 6 cutover — see `legacy-data-import.md`), `frontend` (nginx+SPA), `caddy` (profile `tls`). `docker-compose.override.yml` is local-dev-only (gitignored), mounts host `~/.aws` read-only for local S3 access against a dev bucket with 7-day auto-expiry — correctly separated from prod config.

### Monitoring / alerting / error tracking — Missing entirely
Repo-wide grep for Sentry/Datadog/New Relic/Grafana/Prometheus/Elastic APM/Rollbar/Bugsnag returns zero hits in actual code (one hit only in the gap-analysis doc describing the gap itself). `ILogger` usage exists in 6 service files, but there's no structured-logging framework (no Serilog) and no log aggregation. `infrastructure/README.md` plans only basic CloudWatch EC2 metrics (CPU/disk/status checks) and explicitly skips the CloudWatch Logs agent "for now" — and there's no evidence in-repo that even this was provisioned. **This gap, as flagged in the industry-readiness analysis, remains fully open.**

## Recommended follow-ups
- Pick a minimal error-tracking tool (even just Sentry's free tier) before the next production incident makes the absence painful.
- Confirm whether job-photo uploads (see `jobs-workshop-floor.md`) need their own backup path alongside the DB backup — currently uncovered if running on local file storage.
- If branch/PR ceremony starts feeling worth it (more contributors, want a true merge gate), flip `ci.yml`'s trigger to `pull_request` + turn on branch protection — no workflow changes needed beyond that.
