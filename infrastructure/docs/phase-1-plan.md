# Phase 1 execution plan

The exact plan for standing up Mobmek's first production deployment, in the `mobmek` AWS account.
For the general architecture/cost overview, see `infrastructure/README.md`. This doc is the
specific "here's what gets created, with what values" plan.

**Account:** `649058763120` (`mobmek`, a dedicated AWS account isolated from personal/other AWS
usage, assumed into via the `jun-dev` profile's `OrganizationAccountAccessRole`; the local AWS
CLI profile for it is `mobmek`)
**Region:** `ap-southeast-6` (Asia Pacific — New Zealand, Auckland)

## Resources

Everything except the EC2 instance and its Elastic IP is provisioned. Current monthly cost is
**$0** — both S3 buckets are empty, and versioning, lifecycle rules and encryption are free.

| # | Resource | Status | Detail |
|---|---|---|---|
| 1 | AWS Budgets alert | ✅ done | "My Monthly Cost Budget", $25/mo threshold — free |
| 2 | SSH key pair | ✅ done | `mobmek-prod`, private key saved locally to `~/.ssh/mobmek-prod.pem` |
| 3 | Security group | ✅ done | `mobmek-prod-sg` — SSH (22) from `149.19.25.197/32` only (update if the admin IP changes); HTTP/HTTPS (80/443) open to everyone |
| 4 | IAM role for the instance | ✅ done | `mobmek-prod-ec2-role` (+ instance profile of the same name), granting `mobmek-prod-ec2-policy` — see the policy breakdown below |
| 5 | S3 backups bucket | ✅ done | `mobmek-backups-649058763120`, versioned, all public access blocked, SSE-S3 + bucket keys, lifecycle → Glacier IR at 30d / noncurrent versions expire at 90d / incomplete multipart aborted at 7d |
| 5b | S3 uploads bucket | ✅ done | `mobmek-uploads-649058763120`, same privacy and encryption settings, lifecycle → noncurrent versions expire at 30d / incomplete multipart aborted at 7d. **No Glacier transition** — photos are read interactively and need to stay in Standard |
| 6 | EC2 instance | ❌ **not created** | `t4g.small`, Ubuntu 24.04 LTS (arm64), boots with Docker + Compose pre-installed via a startup script |
| 7 | Elastic IP | ❌ **not created** | Allocated and attached to the instance |

Cost once the instance is running: **~$19–23/month** (breakdown in `infrastructure/README.md`).
Step 6 is what starts the meter.

### Instance role policy (`mobmek-prod-ec2-policy`, default version `v2`)

| Sid | Grants | On |
|---|---|---|
| `BackupBucketAccess` | `s3:PutObject`, `s3:GetObject`, `s3:ListBucket` | `mobmek-backups-649058763120` |
| `UploadsBucketAccess` | `s3:PutObject`, `s3:GetObject`, `s3:DeleteObject`, `s3:ListBucket` | `mobmek-uploads-649058763120` |
| `SecretsReadAccess` | `ssm:GetParameter`, `ssm:GetParameters`, `ssm:GetParametersByPath` | `arn:aws:ssm:ap-southeast-6:649058763120:parameter/mobmek/*` |

Two deliberate choices in there:

- **`DeleteObject` is granted on uploads but not on backups.** The app genuinely deletes uploads
  (`S3FileStorage.DeleteAsync`, when a photo or receipt is removed), but nothing should ever
  delete a backup — lifecycle handles expiry. A compromised instance can therefore write backups
  but not erase them.
- **`ListBucket` on uploads is load-bearing, not incidental.** S3 only answers `404` for a
  missing object when the caller holds `s3:ListBucket`; without it a missing key returns `403`,
  which `S3FileStorage.OpenReadAsync` cannot distinguish from a broken policy — so every file
  would read as an error. Verified with `aws iam simulate-principal-policy`.

### Still outstanding (not blocking the instance launch)

- **No SSM parameters exist yet.** The policy already grants read access to `/mobmek/*`, but the
  parameters themselves need creating: `POSTGRES_PASSWORD`, `RESEND_API_KEY`,
  `GOOGLE_CALENDAR_CREDENTIALS_JSON`, `GOOGLE_CALENDAR_ID`, `BOOTSTRAP_ADMIN_*`.
- **No production migration tooling.** The deploy flow calls for running EF Core migrations as a
  separate step, but the runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) has no SDK and no
  `dotnet-ef`, and `Program.cs` gates `Database.Migrate()` to Development only. Pick one of:
  an idempotent SQL script (`dotnet ef migrations script --idempotent`, reviewable before it
  runs — the best fit while this is still a test deployment), a migration bundle
  (`dotnet ef migrations bundle`, a self-contained arm64 executable), or a one-shot SDK-image
  Compose service.

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
