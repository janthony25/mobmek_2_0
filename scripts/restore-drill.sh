#!/usr/bin/env bash
#
# Proves a backup actually works — restores it into a scratch Postgres container (never the
# real "db" service) and runs a few sanity checks. "A file exists in S3" is not the same claim
# as "this backup can bring the business back", and the only way to know the second one is true
# is to actually do it. Safe to run anytime; touches nothing but a disposable container.
#
# Usage:
#   ./scripts/restore-drill.sh                  # drills the most recent nightly backup
#   ./scripts/restore-drill.sh <s3-key>          # drills a specific one, e.g. for an old date

set -euo pipefail

BUCKET="mobmek-backups-649058763120"
REGION="ap-southeast-6"
SCRATCH_CONTAINER="mobmek_restore_drill"
LOCAL_FILE="/tmp/restore-drill.sql.gz"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }
cleanup() { docker rm -f "$SCRATCH_CONTAINER" >/dev/null 2>&1 || true; rm -f "$LOCAL_FILE"; }
trap cleanup EXIT

S3_KEY="${1:-}"
if [[ -z "$S3_KEY" ]]; then
  step "No backup specified — finding the most recent nightly one"
  S3_KEY=$(aws s3api list-objects-v2 --bucket "$BUCKET" --prefix "nightly/" --region "$REGION" \
    --query 'sort_by(Contents, &LastModified)[-1].Key' --output text)
  [[ "$S3_KEY" != "None" && -n "$S3_KEY" ]] || fail "No backups found under s3://$BUCKET/nightly/ — has the cron ever run?"
fi
ok "Drilling s3://$BUCKET/$S3_KEY"

step "Downloading"
aws s3 cp "s3://$BUCKET/$S3_KEY" "$LOCAL_FILE" --region "$REGION"

step "Starting a disposable, empty Postgres — never the real db container"
docker run -d --rm --name "$SCRATCH_CONTAINER" \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=mobmek postgres:17 >/dev/null
printf '    Waiting for it to accept connections'
for _ in $(seq 1 30); do
  docker exec "$SCRATCH_CONTAINER" pg_isready -U postgres -d mobmek >/dev/null 2>&1 && break
  printf '.'; sleep 1
done
echo
docker exec "$SCRATCH_CONTAINER" pg_isready -U postgres -d mobmek >/dev/null 2>&1 \
  || fail "Scratch Postgres never came up"
ok "Ready"

step "Restoring into it"
gunzip -c "$LOCAL_FILE" | docker exec -i "$SCRATCH_CONTAINER" psql -U postgres -d mobmek -v ON_ERROR_STOP=1 \
  > /tmp/restore-drill-apply.log 2>&1 \
  || fail "Restore failed — see /tmp/restore-drill-apply.log"
ok "Restore completed with no errors"

step "Sanity checks"
TABLE_COUNT=$(docker exec "$SCRATCH_CONTAINER" psql -U postgres -d mobmek -tAc \
  "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';")
[[ "$TABLE_COUNT" -gt 10 ]] || fail "Only $TABLE_COUNT tables after restore — that's not a real schema."
ok "$TABLE_COUNT tables present"

ADMIN_COUNT=$(docker exec "$SCRATCH_CONTAINER" psql -U postgres -d mobmek -tAc \
  "SELECT count(*) FROM \"AspNetUsers\";" 2>/dev/null || echo 0)
ok "$ADMIN_COUNT account(s) restored"

step "Drill passed"
echo "    s3://$BUCKET/$S3_KEY is a real, restorable backup — not just a file that exists."
