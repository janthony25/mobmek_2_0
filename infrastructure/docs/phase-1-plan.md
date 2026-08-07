# Phase 1 execution plan

The exact plan for standing up Mobmek's first production deployment, in the `mobmek` AWS account.
For the general architecture/cost overview, see `infrastructure/README.md`. This doc is the
specific "here's what gets created, with what values" plan — nothing below has been provisioned
yet.

**Account:** `649058763120` (`mobmek`, a dedicated AWS account isolated from personal/other AWS
usage, assumed into via the `jun-dev` profile's `OrganizationAccountAccessRole`)
**Region:** `ap-southeast-6` (Asia Pacific — New Zealand, Auckland)

## Resources to create

| # | Resource | Detail |
|---|---|---|
| 1 | AWS Budgets alert | $25/mo threshold, emails on breach — free |
| 2 | SSH key pair | `mobmek-prod`, private key saved locally to `~/.ssh/mobmek-prod.pem` |
| 3 | Security group | SSH (22) from admin IP only (current: `149.19.25.197` — update if it changes); HTTP/HTTPS (80/443) open to everyone |
| 4 | IAM role for the instance | Minimal permissions only (S3 backup bucket + SSM read) — not full admin |
| 5 | S3 bucket | `mobmek-backups-649058763120`, versioned, private, lifecycle → Glacier Instant Retrieval after 30 days |
| 6 | EC2 instance | `t4g.small`, Ubuntu 24.04 LTS (arm64), boots with Docker + Compose pre-installed via a startup script |
| 7 | Elastic IP | Allocated and attached to the instance |

Cost once running: **~$19–23/month** (breakdown in `infrastructure/README.md`). Steps 1–5 and 7
are free or near-free on their own — step 6 (the EC2 instance) is what starts the meter running.

## Operational notes for this deployment

**`docker compose down -v` wipes the database.** It deletes the named volumes, including
`mobmek_pgdata` — not just the containers. `docker compose down` (no `-v`) is always safe. Treat
`-v` as a command that doesn't exist on this box, except in the one deliberate case below.

**Database: Docker now, RDS at go-live.** Postgres stays in the same Compose stack (not RDS, not
a native OS install) while this deployment is used for testing only — that keeps prod identical
to local dev, which is the whole point of Phase 1's simplicity. The nightly S3 backup is the
safety net against the `-v` risk in the meantime. Switch the database to RDS (see
`infrastructure/README.md` → Growth path → Upgrade 1, ~$19/mo more) at the point this stops being
a test deployment and becomes the real system staff/customers rely on — treat the complete legacy
data import (below) landing as that trigger point, not something to build speculatively now.

**Swapping in the final legacy data.** The importer (`docs/legacy-import-design.md`) is
insert-only on re-run — it skips rows already imported rather than updating them. So once the
complete `.bak` is ready, don't just re-run the importer on top of the current incomplete test
data. Instead:

1. Copy the complete `.bak` onto the box, replacing the one in `legacy-backup/`.
2. `docker compose down -v` to wipe the test data (safe *only* as long as no real, non-imported
   work has been created directly in the app yet).
3. Bring the DB back up, run migrations, run `./scripts/setup-legacy-data.sh` against the new
   `.bak`.
4. Restart the app containers.

Do this **before** staff start using the deployment day-to-day — once real work exists in the
database, step 2 is no longer safe.
