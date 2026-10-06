# Phase 1 execution plan

The exact plan for standing up Mobmek's first production deployment, in the `mobmek` AWS account.
For the general architecture/cost overview, see `infrastructure/README.md`. This doc is the
specific "here's what gets created, with what values" plan.

**Account:** `649058763120` (`mobmek`, a dedicated AWS account isolated from personal/other AWS
usage, assumed into via the `jun-dev` profile's `OrganizationAccountAccessRole`; the local AWS
CLI profile for it is `mobmek`)
**Region:** `ap-southeast-6` (Asia Pacific — New Zealand, Auckland)

## Resources

Everything is provisioned, including the instance. Billing has started (~$19–23/month).

| # | Resource | Status | Detail |
|---|---|---|---|
| 1 | AWS Budgets alert | ✅ done | "My Monthly Cost Budget", $25/mo threshold — free |
| 2 | SSH key pair | ✅ done | `mobmek-prod`, ed25519, private key at `~/.ssh/mobmek-prod.pem`. Recreated once already — the original's private half was never saved anywhere, so the orphaned AWS key pair was deleted and replaced. If this file is ever lost again, the fix is the same: delete and recreate, there is no recovery path for a lost private key. |
| 3 | Security group | ✅ done | `mobmek-prod-sg` — SSH (22) from `149.19.25.237/32` only (update if the admin IP changes — it already has once); HTTP/HTTPS (80/443) open to everyone |
| 4 | IAM role for the instance | ✅ done | `mobmek-prod-ec2-role` (+ instance profile of the same name), granting `mobmek-prod-ec2-policy` — see the policy breakdown below |
| 5 | S3 backups bucket | ✅ done | `mobmek-backups-649058763120`, versioned, all public access blocked, SSE-S3 + bucket keys, lifecycle → Glacier IR at 30d / noncurrent versions expire at 90d / incomplete multipart aborted at 7d |
| 5b | S3 uploads bucket | ✅ done | `mobmek-uploads-649058763120`, same privacy and encryption settings, lifecycle → noncurrent versions expire at 30d / incomplete multipart aborted at 7d. **No Glacier transition** — photos are read interactively and need to stay in Standard |
| 6 | EC2 instance | ✅ done | `i-0497048f747fdd563`, `t4g.small`, Ubuntu 24.04 LTS arm64 (official Canonical AMI, resolved via SSM public parameter, not hand-picked), `ap-southeast-6c`. IMDSv2 required (`HttpTokens=required`). 30GB gp3 root volume. Docker + Compose plugin installed and verified (`docker run hello-world` succeeded; `docker ps` works for the `ubuntu` user with no `sudo`). |
| 7 | Elastic IP | ✅ done | `3.102.246.171`, associated with the instance |

### A bug in the original user-data script, for the record

The first boot's user-data bundled `awscli` into the same `apt-get install` line as the Docker
packages. `awscli` has no installation candidate via apt on this image — Ubuntu's `apt-get
install` fails the whole command if any package is unavailable, so **Docker silently never
installed** on first boot; cloud-init's own status correctly showed `error`, which is what caught
it. Fixed by SSHing in and re-running the install without `awscli` (which was never actually
needed — the instance role already hands any AWS SDK or CLI call credentials automatically, so
having the `aws` CLI binary itself on the box is a convenience, not a requirement). Worth knowing
if this instance is ever rebuilt from the same user-data: either drop `awscli` from it, or expect
to repeat this fix.

## Next: actually deploying the app

Nothing has been deployed onto the box yet — Docker is ready, the app isn't there. Still ahead:

1. `git clone` this repo onto the box (now possible — everything is pushed to `origin/main`)
2. Pull the SSM secrets into `.env` (all 6 exist — see the SSM section below)
3. Set `FILE_STORAGE_PROVIDER=S3` / `FILE_STORAGE_S3_BUCKET=mobmek-uploads-649058763120` (not
   the `-dev` bucket), `ASPNETCORE_ENVIRONMENT=Production`, `FRONTEND_BASE_URL`
4. `docker compose up -d --build`
5. Run `scripts/generate-migration-script.sh` locally and apply it on the box (this is a brand
   new database — this step creates the *entire* schema, not just an incremental update)
6. Verify `http://3.102.246.171` loads and Swagger is unreachable

TLS, the backup cron, and a domain are still open — see `infrastructure/README.md`'s checklist.

### Instance role policy (`mobmek-prod-ec2-policy`, default version `v3`)

| Sid | Grants | On |
|---|---|---|
| `BackupBucketAccess` | `s3:PutObject`, `s3:GetObject`, `s3:ListBucket` | `mobmek-backups-649058763120` |
| `UploadsBucketAccess` | `s3:PutObject`, `s3:GetObject`, `s3:DeleteObject`, `s3:ListBucket` | `mobmek-uploads-649058763120` |
| `SecretsReadAccess` | `ssm:GetParameter`, `ssm:GetParameters`, `ssm:GetParametersByPath` | `arn:aws:ssm:ap-southeast-6:649058763120:parameter/mobmek/*` |
| `SecretsDecrypt` | `kms:Decrypt` | the account's default SSM key (`alias/aws/ssm`) |

Three deliberate choices in there:

- **`DeleteObject` is granted on uploads but not on backups.** The app genuinely deletes uploads
  (`S3FileStorage.DeleteAsync`, when a photo or receipt is removed), but nothing should ever
  delete a backup — lifecycle handles expiry. A compromised instance can therefore write backups
  but not erase them.
- **`ListBucket` on uploads is load-bearing, not incidental.** S3 only answers `404` for a
  missing object when the caller holds `s3:ListBucket`; without it a missing key returns `403`,
  which `S3FileStorage.OpenReadAsync` cannot distinguish from a broken policy — so every file
  would read as an error. Verified with `aws iam simulate-principal-policy`.
- **`SecretsDecrypt` is not optional.** `ssm:GetParameter --with-decryption` on a `SecureString`
  needs `kms:Decrypt` on the key it was encrypted with, as a *separate* grant from the SSM
  permissions above — without it, the parameter still "fetches" successfully but decryption
  fails, which would only have surfaced mid-deploy. Caught via `simulate-principal-policy`
  showing `implicitDeny` before launch, not after.

### SSM parameters (all created, all under `/mobmek/*`)

| Name | Type | Source |
|---|---|---|
| `POSTGRES_PASSWORD` | SecureString | freshly generated — not the local dev placeholder |
| `BOOTSTRAP_ADMIN_EMAIL` | String | `justforvalo25@gmail.com` |
| `BOOTSTRAP_ADMIN_PASSWORD` | SecureString | freshly generated — not the local dev placeholder |
| `RESEND_API_KEY` | SecureString | same key as local dev (same Resend account, same domain) |
| `GOOGLE_CALENDAR_ID` | String | same calendar as local dev |
| `GOOGLE_CALENDAR_CREDENTIALS_JSON` | SecureString | same service account as local dev |

Verified end-to-end, not just that the policy looks right: `aws ssm get-parameter --with-decryption`
against the real KMS key succeeded and returned the correct value.

## Running migrations in production

The runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) has no SDK and no `dotnet-ef`, and
`Program.cs` deliberately gates `Database.Migrate()` to Development only — so nothing on the box
can apply a schema change on its own. This is **not** a reason to move to RDS: wherever Postgres
runs, something still has to generate and run the migration SQL. RDS changes where the database
lives, not how you apply a change to it.

`scripts/generate-migration-script.sh` generates the SQL on your own machine (where the SDK
already is) via `dotnet ef migrations script --idempotent`, which wraps every migration in a
guard against `__EFMigrationsHistory` — safe to run against a database on any prior migration,
not just the one you tested against. Verified by running the same generated script against both
the current dev DB (no-op, exit 0) and a brand-new empty Postgres (reproduced the full schema —
every table matched except `legacy_import_map`, which belongs to the separate MSSQL import tool,
not EF).

```bash
./scripts/generate-migration-script.sh    # writes ./migrate.sql (gitignored — regenerate, don't commit)
less migrate.sql                          # skim for anything beyond additive changes

scp migrate.sql <box>:~/
ssh <box>
docker exec mobmek_db pg_dump -U postgres mobmek | gzip > "pre-migrate-$(date +%Y%m%d%H%M).sql.gz"
docker compose exec -T db psql -U postgres -d mobmek -v ON_ERROR_STOP=1 < migrate.sql
```

The `pg_dump` line is the safety net until the nightly-backup-to-S3 and restore-drill checklist
items above are done — an additive migration (new table, new nullable column) is low-risk, but
anything that drops or rewrites data should wait for a tested restore path before it runs
anywhere real.

## File storage configuration

Uploaded files — job photos, transaction receipts, the business logo — go through `IFileStorage`.
The provider is config-driven (`FileStorage:Provider`, see `.env.example`):

```bash
FILE_STORAGE_PROVIDER=S3
FILE_STORAGE_S3_BUCKET=mobmek-uploads-649058763120
FILE_STORAGE_S3_REGION=            # blank on EC2 — the instance's own region is used
```

No AWS keys go in `.env`: the SDK's default credential chain picks up the instance role. The API
refuses to start if the provider is `S3` with no bucket set, or if the provider name is
unrecognised — a typo must not silently fall back to local disk.

**Local disk is not a valid production setting.** `docker compose up -d --build` discards the
container filesystem, so uploads stored locally are destroyed on every deploy *while their
database rows survive* — the app then shows a full list of photos that all fail to load. A
`mobmek_uploads` volume is mounted in `docker-compose.yml` to stop that happening in local dev,
but S3 is the actual fix for a deployment.

**Copy pre-existing local files up before switching the provider.** Keys are byte-identical
between the two backends (both come from `StorageKeys`), so no data needs rewriting — but the
bytes have to be moved, or existing rows point at objects that aren't there:

```bash
aws s3 sync ./uploads s3://mobmek-uploads-649058763120/ --profile mobmek
```

### Testing S3 from a laptop

There is a third bucket, `mobmek-uploads-dev-649058763120`, for local development only: same
privacy and encryption settings, no versioning, and objects auto-expire after 7 days so test
junk cleans itself up. Local dev points here, never at the production bucket, so a local test —
or a local delete — can't touch real customer files.

The container has no credentials of its own, so `docker-compose.override.yml` (gitignored,
per-developer) mounts `~/.aws` read-only and sets `AWS_PROFILE=mobmek`; the SDK performs the
role assumption itself. Nothing like this exists on EC2 — the instance role is supplied by
instance metadata, with no config at all.

That difference is the reason `AWSSDK.SecurityToken` is referenced in `MobmekApi.csproj`:
assuming a role needs STS, and the AWS SDK loads that assembly **by reflection**, so a missing
reference is not a build error — it appears at runtime on the first upload as
`Assembly AWSSDK.SecurityToken could not be found or loaded`. Production never takes that code
path, so this is a development-only dependency that nonetheless has to ship in the image.

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
