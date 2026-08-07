# Infrastructure — AWS deployment plan

Plan for taking Mobmek from local Docker Compose to a live AWS deployment, written for a
single-shop SMB budget. Nothing here is provisioned yet — this is the plan to execute against,
and the checklist doubles as a punch list.

**Which phase do you actually need? Phase 1, right now — that's it.** Phase 1 is a complete,
real production deployment: staff and customers reach it from any device over the internet like
any normal website, at ~$15–20/month. "Growth path" below is a reference menu for *later*, each
item adopted independently and only when a specific trigger happens — it is not a second
deployment you also need to set up today, and adopting one item from it doesn't require adopting
the rest.

The guiding rule: **use the cheapest AWS service that meets the actual requirement, not the
"proper" one.** A single EC2 box running the exact same `docker-compose.yml` this repo already
has, backed up to S3, is a legitimate production setup at this scale — not a shortcut to be
embarrassed about. Most of what makes AWS expensive (ALB, NAT Gateway, RDS Multi-AZ, Fargate)
solves problems a one-shop system doesn't have yet.

Region: **ap-southeast-2 (Sydney)** — closest AWS region to NZ, no AWS region exists in NZ itself.

## Cost summary — every option, one table

Everything below Phase 1 is optional and only added if/when its trigger happens (see each
section for the trigger). "Total" is the full monthly bill if you had *only* that row's changes
on top of Phase 1 — rows don't stack with each other unless stated.

| Setup | What's running | Total /month (USD) |
|---|---|---|
| **Phase 1 (do this now)** | Everything on 1 EC2 box | **~$15–20** |
| Phase 1 + RDS single-AZ | EC2 (frontend+api) + RDS for the DB | ~$30–38 |
| Phase 1 + RDS Multi-AZ | Same, with DB failover | ~$45–55 |
| Everything (Fargate+ALB+RDS+CloudFront+CI/CD) | Full "standard" AWS shape, no EC2 at all | ~$56–72 (single-AZ RDS) to ~$85–100 (Multi-AZ RDS) |

Full breakdown of each row is in "Growth path" further down.

---

## Phase 1 — minimum viable production (set this up now)

One EC2 instance runs the *same* `docker-compose.yml` already in this repo (db + api + frontend
containers) — no architecture change from local dev, which is exactly what keeps this cheap and
easy to reason about.

### How it connects

```mermaid
flowchart TB
    User["Staff / customer browser<br/>(any device, anywhere)"] -->|"HTTPS :443"| DNS["Route 53<br/>app.mobmek.co.nz"]
    DNS --> EIP["Elastic IP<br/>(static public address)"]
    EIP --> EC2

    subgraph EC2["EC2 t4g.small — the only server"]
        direction TB
        Caddy["Caddy / certbot<br/>TLS termination · :80 / :443"]
        FE["frontend container<br/>nginx + React SPA"]
        API["api container<br/>ASP.NET Core · :8080"]
        DB[("db container<br/>Postgres 17 · :5432")]
        Cron["cron job<br/>nightly pg_dump"]

        Caddy -->|"/"| FE
        Caddy -->|"/api/*"| API
        FE -->|"/api/* proxy<br/>(nginx.conf, unchanged)"| API
        API -->|"Docker network,<br/>not public"| DB
        Cron --> DB
    end

    Cron -->|"upload via IAM<br/>instance role"| S3[("S3 bucket<br/>nightly backups only")]
    SSM["SSM Parameter Store<br/>(secrets)"] -.->|"pulled into .env<br/>at deploy time"| EC2

    style DB fill:#2d3748,stroke:#718096,color:#fff
    style S3 fill:#2d3748,stroke:#718096,color:#fff
```

Everything — web server, API, database — lives in containers on **one box**. The only things
public are ports 80/443; Postgres and the API's raw port never leave the Docker network, same as
local dev today. SSH (22) is restricted to your own IP for admin access only — it's not how
anyone *uses* the app (see the earlier point: end users never touch the server directly, they
just hit the domain in a browser).

### What to provision

| Piece | Service | Notes |
|---|---|---|
| Compute | **EC2**, 1x `t4g.small` (Graviton/arm64, 2 vCPU/2GB) | Amazon Linux 2023 or Ubuntu 24.04 + Docker + Compose plugin. arm64 is ~20% cheaper than x86 and the Dockerfiles' base images (`.NET`, `node`, `nginx`, `postgres`) all publish arm64 builds. |
| Public IP | **Elastic IP** | Free while attached to a running instance. Static IP for DNS. |
| Storage | **EBS gp3**, 30GB | Root volume + Postgres data volume. |
| DNS | **Route 53** hosted zone | A record → Elastic IP. |
| TLS | **Caddy** (reverse-proxied in front of the existing nginx container) or **certbot** | Free Let's Encrypt certs, no ACM/ALB needed for a single box. |
| Backups | **S3** bucket, versioned, lifecycle → Glacier Instant Retrieval after 30 days | Nightly `pg_dump` from a cron job on the box. This is the fix for the "no backup/DR" gap flagged as highest-priority in `docs/industry-readiness-gap-analysis.md` §6.1 — do this first, before anything else here. |
| Secrets | **SSM Parameter Store** (Standard tier, free) | Holds `RESEND_API_KEY`, `GOOGLE_CALENDAR_CREDENTIALS_JSON`, `POSTGRES_PASSWORD`, etc. Pulled into `.env` at deploy time instead of hand-copied. |
| IAM | One instance role (S3 backup bucket + SSM read only), one admin IAM user with MFA | Never use root account keys day-to-day. |
| Monitoring | **CloudWatch** basic EC2 metrics (CPU/disk/status checks) | Free tier alarms. Skip CloudWatch Logs agent for now — `docker compose logs` is enough at one shop's traffic, and log ingestion is billed per GB. |
| Budget guardrail | **AWS Budgets** alert at ~$25/mo | Free. Catches a misconfigured resource before it becomes a surprise bill. |

**Security group:** only 80/443 open to the world, 22 (SSH) restricted to your own IP(s). Postgres
(5433 in local compose) and the API's raw 8080 are **not** exposed publicly — nginx is still the
only public entry point, proxying `/api` internally exactly like it does today.

### Rough monthly cost (ap-southeast-2, USD, approximate)

| Item | Cost |
|---|---|
| EC2 t4g.small on-demand | ~$12–14 |
| EC2 t4g.small, 1-yr no-upfront reserved (once stable) | ~$8 |
| EBS gp3 30GB | ~$2.50 |
| Elastic IP (attached) | $0 |
| S3 backups (<5GB) | <$1 |
| Route 53 hosted zone | ~$0.50 + $0.40/million queries |
| Data transfer out | first 100GB/mo free, then $0.09/GB |
| **Total** | **~$15–20/month** |

---

## What we're deliberately NOT doing in Phase 1 (cost discipline)

- **No RDS.** Postgres in the same Compose stack on the EC2 box, backed up nightly to S3, is fine
  at this scale and saves ~$15–30/mo over even a single-AZ `db.t4g.micro`.
- **No ALB / ECS / Fargate.** One box running the Compose file already tested locally is the same
  shape in prod — no new orchestration layer to learn or pay for.
- **No CloudFront.** The nginx container already serves the SPA fine at this traffic volume.
- **No NAT Gateway.** Default VPC, public subnet, one instance — a NAT Gateway alone is ~$32/mo
  for a private-subnet pattern this deployment doesn't need.
- **No Secrets Manager.** $0.40/secret/month adds up for no benefit over SSM Parameter Store
  Standard tier, which is free and does the same job here.
- **No ECR.** Building the image directly on the box (`docker compose up -d --build`) is simpler
  and free; only worth it once there's CI doing the build elsewhere (Growth path).

## Services checklist (Phase 1)

- [ ] AWS account + AWS Budgets alert (~$25/mo threshold)
- [ ] IAM admin user (MFA) + limited instance role (S3 backup bucket + SSM read only)
- [ ] EC2 `t4g.small` in the default VPC, Docker + Compose plugin installed
- [ ] Elastic IP attached to the instance
- [ ] Security group: 22 (your IP only), 80, 443 — nothing else public
- [ ] Route 53 hosted zone + A record → Elastic IP
- [ ] TLS via Caddy or certbot
- [ ] S3 bucket (versioned, lifecycle → Glacier IR @ 30d, block public access) for DB backups
- [ ] Nightly cron: `pg_dump` → upload to S3, via the instance role (scoped to that bucket only)
- [ ] A tested **restore** drill, not just a backup script — untested backups aren't backups
- [ ] SSM Parameter Store entries for the secrets currently in `.env.example`
- [ ] `ASPNETCORE_ENVIRONMENT=Production` set (dev auto-migrates + exposes Swagger; prod must not)
- [ ] EF Core migrations run as a deliberate separate step post-deploy (see `mobmek_api/CLAUDE.md`)

## Deploy flow (Phase 1, manual — no CI/CD yet)

1. SSH to the instance.
2. `git clone`/`git pull` this repo.
3. Pull secrets from SSM into `.env` (or hand-maintain `.env`, root-only permissions — acceptable
   at this scale as long as it's never committed).
4. Set `ASPNETCORE_ENVIRONMENT=Production` and `FRONTEND_BASE_URL` to the real domain.
5. `docker compose up -d --build`.
6. Run EF Core migrations as a separate step (production does not auto-migrate on startup).
7. Verify `https://<domain>` loads and that Swagger is not reachable in prod.

---

## File storage / S3 for uploads (not backups) — when it's actually needed

There's no attachment/photo feature on `Job`/`JobItem` yet (tracked as gap 2.1 in
`docs/industry-readiness-gap-analysis.md` — DVI photos). The API already has this abstracted:
`IFileStorage` (`mobmek_api/src/MobmekApi/Services/IFileStorage.cs`) with a local-disk
implementation (`LocalFileStorage.cs`) whose storage keys (`yyyy/MM/{guid}{ext}`) are already
shaped to drop straight into S3 object keys.

**Until that feature exists, S3 in this deployment is backup storage only** — don't provision an
uploads bucket ahead of the feature that needs it. When DVI photos (or any file upload) land, add
an `S3FileStorage : IFileStorage` implementation and a second bucket (private, not public-read;
serve via signed URLs or through the API) — no other code changes required, by design.

---

## Growth path — independent upgrades for later, not a package deal

Everything below is a **menu, not a bundle**. Each row solves one specific problem, has its own
trigger, and can be adopted on its own — taking one doesn't obligate you to take the others. Don't
adopt any of them speculatively; wait for the trigger.

### Upgrade 1 — move the database to RDS (compute stays on EC2)

The most common first move, and it **does not require the rest of this section** — the API
container just points its connection string at an RDS endpoint instead of the local `db` service;
everything else in Phase 1 (EC2, nginx, Elastic IP, Route 53) stays exactly as is.

```mermaid
flowchart TB
    User["Staff / customer browser"] -->|"HTTPS :443"| DNS["Route 53"] --> EIP["Elastic IP"] --> EC2

    subgraph EC2["EC2 t4g.small — same box as Phase 1, minus the db container"]
        direction TB
        Caddy["Caddy / certbot"]
        FE["frontend container"]
        API["api container"]
        Caddy --> FE
        Caddy -->|"/api/*"| API
        FE -->|"/api/* proxy"| API
    end

    API -->|"Npgsql connection string<br/>→ RDS endpoint (VPC-internal)"| RDS[("RDS Postgres<br/>single-AZ db.t4g.micro")]
    RDS -->|"automated,<br/>no cron needed"| Snap[("RDS automated<br/>backups / snapshots")]

    style RDS fill:#2d3748,stroke:#718096,color:#fff
    style Snap fill:#2d3748,stroke:#718096,color:#fff
```

**Trigger — do this when any one of these becomes true:**
- A real restore drill from the S3 `pg_dump` backup goes badly, is slow, or you're just not
  confident in it — RDS's automated, point-in-time backup/restore replaces that whole workflow.
- Downtime from restarting/rebuilding the EC2 box (which currently takes the DB down with
  everything else) becomes unacceptable during business hours — RDS Multi-AZ adds failover, at
  a higher tier (~$25–30+/mo vs. single-AZ's ~$13/mo).
- You adopt Upgrade 2 (multiple compute instances) — at that point RDS stops being optional,
  since a Postgres container on one box can't be shared across several app servers.

**Cost:**

| Item | /month |
|---|---|
| EC2 t4g.small (frontend + api only, db container removed) | ~$12–14 (unchanged) |
| RDS `db.t4g.micro`, single-AZ, 20GB gp3 | ~$15–18 |
| Everything else (EBS, Route 53, S3, Elastic IP) | ~$3–4 |
| **Total (single-AZ)** | **~$30–38** |
| RDS Multi-AZ instead (adds failover) | ~$28–32 in place of the single-AZ RDS line |
| **Total (Multi-AZ)** | **~$45–55** |

### Upgrade 2 — ECS Fargate + ALB (compute, independent of where the DB lives)

Only needed for zero-downtime deploys or when one instance can't handle the load — not something
a single shop's traffic requires today. In practice this needs Upgrade 1 (RDS) alongside it —
Fargate tasks are ephemeral, so they can't host a Postgres container the way EC2 does.

**Cost (replaces the EC2 instance; assumes RDS single-AZ is already in place from Upgrade 1):**

| Item | /month |
|---|---|
| Application Load Balancer | ~$16–20 |
| Fargate tasks (api + frontend, small size) | ~$20–25 |
| RDS single-AZ (carried over from Upgrade 1) | ~$15–18 |
| Everything else (Route 53, S3, EBS→gone, Elastic IP→gone) | ~$1–2 |
| **Total** | **~$52–65** |

### Upgrade 3 — CloudFront + S3 static hosting for the SPA

Faster global delivery than one nginx container. Low priority — nginx already serves the SPA fine
at this traffic volume.

**Cost:** ~$2–5/month added (CloudFront has a free tier for the first 12 months and low usage
here; S3 storage for a built SPA is pennies).

### Upgrade 4 — ECR + GitHub Actions CI/CD

Automated build/push/deploy, addressing gap 6.2 in the gap-analysis doc (no CI/CD today).
Independent of the other three — worth adopting whenever manual deploys start feeling risky or
frequent, regardless of what compute/DB setup is in place.

**Cost:** ~$1–2/month added (ECR image storage is ~$0.10/GB; GitHub Actions minutes are free-tier
at this usage level for a small repo).

### If you eventually adopt all four together

This is what the fully "standard" AWS shape looks like — shown for reference, not as a target to
build toward on a specific timeline:

```mermaid
flowchart TB
    User["Staff / customer browser"] -->|"HTTPS :443"| DNS["Route 53"]
    DNS --> CF["CloudFront"]
    CF -->|"/api/*"| ALB["Application Load Balancer<br/>ACM TLS cert"]
    CF -->|"static assets"| S3FE[("S3 — built SPA")]

    ALB --> API1["Fargate task<br/>api"]
    ALB --> API2["Fargate task<br/>api (replica)"]

    API1 --> RDS[("RDS Postgres<br/>Multi-AZ")]
    API2 --> RDS
    API1 --- S3U[("S3 — uploads<br/>e.g. DVI photos")]
    API2 --- S3U

    ECR["ECR<br/>image registry"] -.->|"pull image"| API1
    ECR -.->|"pull image"| API2
    GA["GitHub Actions<br/>CI/CD"] -->|"build & push"| ECR
    GA -->|"deploy"| ALB

    style RDS fill:#2d3748,stroke:#718096,color:#fff
    style S3FE fill:#2d3748,stroke:#718096,color:#fff
    style S3U fill:#2d3748,stroke:#718096,color:#fff
```

**Cost (running total, single-AZ RDS):**

| Item | /month |
|---|---|
| ALB + Fargate tasks + RDS single-AZ (Upgrades 1+2 combined) | ~$52–65 |
| + CloudFront/S3 static hosting (Upgrade 3) | +~$2–5 |
| + ECR + CI/CD (Upgrade 4) | +~$1–2 |
| **Total (single-AZ RDS)** | **~$56–72** |
| **Total (Multi-AZ RDS instead)** | **~$85–100** |

Worth it once the business outgrows one box — not before, and not all at once. Each piece is
still adopted independently, per its own trigger above.

---

## Open questions

- Domain name to register/point (Route 53 or existing registrar + Route 53 as DNS).
- Who holds the AWS root account (billing owner, MFA device)?
- Any NZ data-residency requirement for customer PII? There's no AWS NZ region — Sydney
  (ap-southeast-2) is the nearest option and is what this plan assumes.
