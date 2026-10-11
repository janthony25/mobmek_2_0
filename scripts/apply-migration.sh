#!/usr/bin/env bash
#
# Applies migrate.sql (from ./scripts/generate-migration-script.sh) to EC2's production
# database. Previously this was a fully manual SSH dance (see that script's old header
# comment) where taking a pg_dump backup first was only ever a documented recommendation —
# easy to skip under pressure or simply forget. This script does the same steps but the
# backup is not optional: it always runs, and the script aborts before touching anything
# if the backup looks too small to be real.
#
# Usage:
#   ./scripts/generate-migration-script.sh   # generates migrate.sql — review it first
#   ./scripts/apply-migration.sh             # backs up, copies, applies, verifies

set -euo pipefail

HOST="ubuntu@3.102.246.171"
KEY="$HOME/.ssh/mobmek-prod.pem"
REMOTE_DIR="mobmek_2_0"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MIGRATE_FILE="$REPO_ROOT/migrate.sql"

step "Checking for migrate.sql"
[[ -f "$MIGRATE_FILE" ]] || fail "No migrate.sql at $MIGRATE_FILE — run ./scripts/generate-migration-script.sh first."
ok "Found $MIGRATE_FILE ($(wc -l < "$MIGRATE_FILE" | tr -d ' ') lines)"

step "Have you reviewed it?"
read -r -p "    Open it with 'less migrate.sql' if you haven't. Continue? [y/N] " CONFIRM
[[ "$CONFIRM" =~ ^[Yy]$ ]] || fail "Aborted — review migrate.sql first."

step "Copying to $HOST"
scp -i "$KEY" "$MIGRATE_FILE" "$HOST:~/migrate.sql" >/dev/null
ok "Copied"

step "Backing up $HOST's database before applying anything"
BACKUP_NAME="pre-migrate-$(date -u +%Y%m%d%H%M%S).sql.gz"
ssh -i "$KEY" "$HOST" "cd $REMOTE_DIR && docker exec mobmek_db pg_dump -U postgres mobmek | gzip > ~/$BACKUP_NAME"
BACKUP_SIZE=$(ssh -i "$KEY" "$HOST" "stat -c%s ~/$BACKUP_NAME")
[[ "$BACKUP_SIZE" -gt 1024 ]] || fail "Backup ~/$BACKUP_NAME is suspiciously small (${BACKUP_SIZE} bytes) — aborting before applying anything."
ok "Backed up to ~/$BACKUP_NAME on $HOST ($BACKUP_SIZE bytes)"

step "Applying migrate.sql on $HOST"
ssh -i "$KEY" "$HOST" "cd $REMOTE_DIR && docker compose exec -T db psql -U postgres -d mobmek -v ON_ERROR_STOP=1 < ~/migrate.sql"
ok "Applied"

step "Verifying — most recent applied migrations"
ssh -i "$KEY" "$HOST" "cd $REMOTE_DIR && docker compose exec -T db psql -U postgres -d mobmek -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;'"

step "Cleaning up"
ssh -i "$KEY" "$HOST" "rm -f ~/migrate.sql"
rm -f "$MIGRATE_FILE"
ok "Removed migrate.sql locally and on $HOST (it's a regeneratable build artifact, not a source file)"

step "Done"
echo "    Migration applied and verified on $HOST."
echo "    Backup kept at ~/$BACKUP_NAME on $HOST if a rollback is ever needed."
