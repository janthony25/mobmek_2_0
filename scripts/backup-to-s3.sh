#!/usr/bin/env bash
#
# Nightly backup for the deployed box (infrastructure/docs/phase-1-plan.md). Dumps the "mobmek"
# database with pg_dump, compresses it, and uploads it to S3 — never overwriting a previous
# night's file, so a bad backup can never clobber a good one. Relies on the instance's own IAM
# role for S3 access (see mobmek-prod-ec2-policy); nothing here needs AWS credentials of its own.
#
# Intended to run from cron on the box itself, not inside a container — see the crontab entry
# in phase-1-plan.md. Can also be run by hand to take an ad hoc backup (e.g. before a risky
# migration): ./scripts/backup-to-s3.sh
#
# A backup nobody has ever restored isn't a backup — see scripts/restore-drill.sh, which proves
# a specific backup file actually works rather than just existing in S3.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

BUCKET="mobmek-backups-649058763120"
REGION="ap-southeast-6"
# UTC throughout — the box itself runs UTC (see `timedatectl`), and a fixed, unambiguous
# timezone in the filename beats wall-clock time that shifts with daylight saving.
TIMESTAMP="$(date -u +%Y-%m-%d-%H%M%S)"
LOCAL_FILE="/tmp/mobmek-${TIMESTAMP}.sql.gz"
S3_KEY="nightly/mobmek-${TIMESTAMP}.sql.gz"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

step "Dumping mobmek database"
docker compose exec -T db pg_dump -U postgres -d mobmek | gzip > "$LOCAL_FILE"
# A real failure anywhere in that pipeline (pg_dump or gzip) already aborted the script via
# `pipefail` + `set -e` above — this is a second, independent check that what landed on disk
# isn't suspiciously tiny (e.g. a dump of an empty/wrong database), not a replacement for it.
SIZE=$(stat -c%s "$LOCAL_FILE" 2>/dev/null || stat -f%z "$LOCAL_FILE")
[[ "$SIZE" -gt 1024 ]] || fail "Backup file is suspiciously small (${SIZE} bytes) — not uploading it."
ok "Wrote $LOCAL_FILE ($SIZE bytes)"

step "Uploading to s3://$BUCKET/$S3_KEY"
aws s3 cp "$LOCAL_FILE" "s3://$BUCKET/$S3_KEY" --region "$REGION"
ok "Uploaded"

rm -f "$LOCAL_FILE"
ok "Done — local copy removed, backup lives only in S3 now"
