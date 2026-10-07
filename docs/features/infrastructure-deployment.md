# Infrastructure & Deployment

**Last verified:** 2026-10-07 (read `infrastructure/README.md`, `infrastructure/docs/*`, root `docker-compose.yml`/`docker-compose.override.yml`/`Caddyfile`, `scripts/*.sh`, repo-wide grep for CI config and monitoring tools)

## Status summary

| Sub-capability | Status |
|---|---|
| TLS | Working, live |
| Deploy mechanism | **Partial — manual scripted SSH, no CI/CD** |
| Nightly backup | Working |
| Restore drill | Working, genuinely tested |
| Docker Compose service topology | Working |
| Monitoring / alerting / error tracking | **Missing entirely** |

## Details

### TLS — Working
`Caddyfile:6-8`: `workshop.mobmekauto.co.nz { reverse_proxy frontend:80 }`, automatic Let's Encrypt via the `caddy` compose service (profile `tls`, not started by default compose). Commit `470632f` states DNS propagated and a real cert was obtained with a verified `Secure` `Set-Cookie` header over the real domain — self-reported via commit message and `infrastructure/README.md`'s checklist, not independently re-verified live in this audit (no live network check performed).

### Deploy mechanism — Partial, confirms the CI/CD gap is still open
`scripts/publish.sh` SSHs to a hardcoded EC2 host, requires a clean/pushed `main`, runs `git pull && docker compose up -d --build api frontend` remotely, curls the domain for a 200. Explicitly does **not** touch the DB/migrations (that's a separate hand-run step via `scripts/generate-migration-script.sh` + `scp`/`psql`). **No `.github/` directory exists anywhere in the repo** — confirmed no GitHub Actions, and no other CI config (Travis/GitLab/Circle) either. `infrastructure/README.md` itself labels this "Deploy flow (Phase 1, manual — no CI/CD yet)" and lists GitHub Actions + ECR as a future, untriggered upgrade. **The industry-readiness doc's "CI/CD" gap is still fully open** — no build/test gate runs before `main` reaches prod other than the script's own clean-tree check.

### Nightly backup — Working
`scripts/backup-to-s3.sh`: `pg_dump` via `docker compose exec`, gzip, size sanity check, uploads to S3 via instance IAM role (no credentials in the script). Matches `infrastructure/README.md` (daily cron at 13:00 UTC).

### Restore drill — Working, genuinely tested
`scripts/restore-drill.sh` downloads the most recent (or a specified) backup, restores into a **disposable scratch Postgres container** (never the real `db` service), sanity-checks table count and reads `AspNetUsers` count, fails loudly on any step. This is a real drill, not a stub.

### Docker Compose topology — Working
Services: `db` (postgres:17, host port 5433), `api` (ASP.NET, S3/local file-storage toggle, Resend/GCal env vars wired but empty by default), `legacy-mssql` (profile `legacy`, still present pending Phase 6 cutover — see `legacy-data-import.md`), `frontend` (nginx+SPA), `caddy` (profile `tls`). `docker-compose.override.yml` is local-dev-only (gitignored), mounts host `~/.aws` read-only for local S3 access against a dev bucket with 7-day auto-expiry — correctly separated from prod config.

### Monitoring / alerting / error tracking — Missing entirely
Repo-wide grep for Sentry/Datadog/New Relic/Grafana/Prometheus/Elastic APM/Rollbar/Bugsnag returns zero hits in actual code (one hit only in the gap-analysis doc describing the gap itself). `ILogger` usage exists in 6 service files, but there's no structured-logging framework (no Serilog) and no log aggregation. `infrastructure/README.md` plans only basic CloudWatch EC2 metrics (CPU/disk/status checks) and explicitly skips the CloudWatch Logs agent "for now" — and there's no evidence in-repo that even this was provisioned. **This gap, as flagged in the industry-readiness analysis, remains fully open.**

## Recommended follow-ups
- If reliability starts mattering more than deploy speed, GitHub Actions (build+test gate before `publish.sh` is even reachable) is the natural next step — already scoped in `infrastructure/README.md`'s upgrade list.
- Pick a minimal error-tracking tool (even just Sentry's free tier) before the next production incident makes the absence painful.
- Confirm whether job-photo uploads (see `jobs-workshop-floor.md`) need their own backup path alongside the DB backup — currently uncovered if running on local file storage.
