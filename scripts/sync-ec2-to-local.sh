#!/usr/bin/env bash
#
# Pulls a full snapshot of the EC2 production database down into the local dev Postgres,
# so manual testing happens against real production-shaped data before publishing — the
# gap CI can't close (CI only proves build+test pass, not that a change behaves correctly
# against real data/edge cases).
#
# This replaces the ENTIRE local "mobmek" database (schema + data + users + settings) with
# EC2's. After running this, the local admin login is whatever the real EC2 admin account
# is (same email as the usual dev bootstrap admin, justforvalo25@gmail.com, but the real
# production password — not the DevAdmin!2026 dev default, which stops working until the
# next fresh migrate+seed).
#
# A dump of local's current state is taken first (see output path below) in case you want
# the old dev data back — not a guarantee of restorability, just a quick local safety net.
#
# Usage: ./scripts/sync-ec2-to-local.sh

set -euo pipefail

HOST="ubuntu@3.102.246.171"
KEY="$HOME/.ssh/mobmek-prod.pem"
REMOTE_DIR="mobmek_2_0"
TIMESTAMP="$(date -u +%Y%m%d-%H%M%S)"
LOCAL_BACKUP="/tmp/mobmek-local-before-sync-${TIMESTAMP}.sql.gz"
DUMP_FILE="/tmp/mobmek-ec2-snapshot-${TIMESTAMP}.sql.gz"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

step "Checking Docker"
docker info >/dev/null 2>&1 || fail "Docker daemon not reachable. Run: colima start"
[[ "$(docker inspect -f '{{.State.Health.Status}}' mobmek_db 2>/dev/null)" == "healthy" ]] \
  || fail "Local mobmek_db isn't running/healthy. Run: docker compose up -d db"
ok "Local Postgres is up"

step "Backing up local data first (just in case)"
docker exec mobmek_db pg_dump -U postgres mobmek | gzip > "$LOCAL_BACKUP"
ok "Saved $LOCAL_BACKUP"

step "Dumping EC2's production database"
ssh -i "$KEY" "$HOST" 'cd '"$REMOTE_DIR"' && docker compose exec -T db pg_dump -U postgres mobmek' | gzip > "$DUMP_FILE"
SIZE=$(stat -f%z "$DUMP_FILE" 2>/dev/null || stat -c%s "$DUMP_FILE")
[[ "$SIZE" -gt 1024 ]] || fail "EC2 dump is suspiciously small (${SIZE} bytes) — aborting before touching local data."
ok "Downloaded $DUMP_FILE ($SIZE bytes)"

step "Stopping the local api container (releases its DB connections)"
docker compose stop api >/dev/null
ok "Stopped"

step "Dropping and recreating the local mobmek database"
docker exec mobmek_db psql -U postgres -d postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'mobmek' AND pid <> pg_backend_pid();" >/dev/null
docker exec mobmek_db psql -U postgres -d postgres -c "DROP DATABASE mobmek;"
docker exec mobmek_db psql -U postgres -d postgres -c "CREATE DATABASE mobmek;"
ok "Recreated"

step "Restoring EC2's snapshot into local Postgres"
gunzip -c "$DUMP_FILE" | docker exec -i mobmek_db psql -U postgres -d mobmek -v ON_ERROR_STOP=1 >/dev/null
ok "Restored"

step "Restarting the local api container"
docker compose up -d api >/dev/null
sleep 3
ok "Started"

step "Done"
echo "    Local Postgres now matches EC2 exactly (schema + data + users + settings)."
echo "    Local admin login is now the real EC2 admin account (justforvalo25@gmail.com,"
echo "    the real production password) — the old DevAdmin!2026 dev password no longer works"
echo "    against this data until you re-seed a fresh local DB."
echo "    Pre-sync local backup kept at: $LOCAL_BACKUP"
