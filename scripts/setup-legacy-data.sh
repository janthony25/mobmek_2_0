#!/usr/bin/env bash
#
# One-shot legacy data setup (docs/legacy-import-design.md).
#
# Takes a fresh clone from "I have the .bak file" to "all legacy data is in
# local Postgres": starts the databases, restores the MSSQL backup, applies
# EF migrations, and runs the importer. Safe to re-run — every step skips
# work that's already done (the importer itself is idempotent).
#
# Prerequisites:
#   1. Docker running (colima start). Apple Silicon needs `rosetta: true` in
#      ~/.colima/default/colima.yaml for the amd64 SQL Server image.
#   2. The legacy backup saved as legacy-backup/*.bak (real customer data —
#      gitignored, ask the owner for a copy).
#   3. .NET 10 SDK in ~/.dotnet (run ./dotnet-install.sh if missing).
#
# Usage:
#   ./scripts/setup-legacy-data.sh              # full import
#   ./scripts/setup-legacy-data.sh --dry-run    # everything rolled back at the end
#
# Extra arguments are passed through to the importer (e.g. --phase <name>).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Compose reads .env itself; we load it too so sqlcmd uses the same password.
if [[ -f .env ]]; then
  set -a
  # shellcheck disable=SC1091
  source .env
  set +a
fi
MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-LegacyImport!2026}"

LEGACY_DB_NAME="MobileMekaniko"
LEGACY_CONTAINER="mobmek_legacy_mssql"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

sqlcmd() {
  docker exec "$LEGACY_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C "$@"
}

# --- 1. Backup file ----------------------------------------------------------
step "Checking for the legacy .bak file"
BAK_FILE="$(find legacy-backup -maxdepth 1 -name '*.bak' -print -quit 2>/dev/null || true)"
[[ -n "$BAK_FILE" ]] || fail "No .bak file found in legacy-backup/.
The backup contains real customer data and is never committed — ask the owner
for a copy and save it as legacy-backup/<anything>.bak, then re-run this script."
ok "Found $BAK_FILE"

# --- 2. Docker ---------------------------------------------------------------
step "Checking Docker"
docker info >/dev/null 2>&1 || fail "Docker daemon not reachable. Run: colima start"
ok "Docker is up"

step "Starting containers (Postgres + legacy MSSQL)"
docker compose up -d db
docker compose --profile legacy up -d legacy-mssql

printf '    Waiting for Postgres to be healthy'
for _ in $(seq 1 30); do
  [[ "$(docker inspect -f '{{.State.Health.Status}}' mobmek_db 2>/dev/null)" == "healthy" ]] && break
  printf '.'; sleep 2
done
echo
[[ "$(docker inspect -f '{{.State.Health.Status}}' mobmek_db)" == "healthy" ]] \
  || fail "Postgres (mobmek_db) did not become healthy. Check: docker logs mobmek_db"
ok "Postgres ready on localhost:5433"

# SQL Server runs emulated (amd64) on Apple Silicon and can take a while to boot.
printf '    Waiting for SQL Server to accept connections (slow under emulation)'
MSSQL_READY=false
for _ in $(seq 1 60); do
  if sqlcmd -Q "SELECT 1" -b >/dev/null 2>&1; then MSSQL_READY=true; break; fi
  printf '.'; sleep 3
done
echo
$MSSQL_READY || fail "SQL Server ($LEGACY_CONTAINER) not responding after 3 minutes.
Check: docker logs $LEGACY_CONTAINER
On Apple Silicon the container crashes without Rosetta — set 'rosetta: true' in
~/.colima/default/colima.yaml, then: colima restart"
ok "SQL Server ready on localhost:1433"

# --- 3. Restore the backup ---------------------------------------------------
step "Restoring $LEGACY_DB_NAME from the .bak"
DB_EXISTS="$(sqlcmd -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$LEGACY_DB_NAME'" | tr -d '[:space:]')"
if [[ "$DB_EXISTS" == "1" ]]; then
  ok "Database $LEGACY_DB_NAME already restored — skipping (delete it in MSSQL to force a re-restore)"
else
  BAK_IN_CONTAINER="/var/opt/mssql/backup/$(basename "$BAK_FILE")"

  # Build one MOVE clause per file in the backup (data → .mdf, log → .ldf).
  MOVE_CLAUSES="$(sqlcmd -h -1 -W -s '|' \
      -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$BAK_IN_CONTAINER'" \
    | awk -F'|' '$3 == "D" || $3 == "L" {
        ext = ($3 == "D") ? "mdf" : "ldf";
        printf ", MOVE N'\''%s'\'' TO N'\''/var/opt/mssql/data/%s.%s'\''", $1, $1, ext
      }')"
  [[ -n "$MOVE_CLAUSES" ]] || fail "Could not read the file list from the backup. Is the .bak valid?"

  sqlcmd -b -Q "RESTORE DATABASE [$LEGACY_DB_NAME] FROM DISK = N'$BAK_IN_CONTAINER' WITH REPLACE$MOVE_CLAUSES" \
    || fail "RESTORE DATABASE failed — see output above."
  ok "Restored $LEGACY_DB_NAME"
fi

# --- 4. .NET 10 --------------------------------------------------------------
step "Checking .NET 10 SDK"
if [[ -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi
dotnet --list-sdks 2>/dev/null | grep -q '^10\.' \
  || fail ".NET 10 SDK not found. Install it into ~/.dotnet with: ./dotnet-install.sh --channel 10.0"
ok ".NET 10 found"

# --- 5. Target schema --------------------------------------------------------
step "Applying EF Core migrations to Postgres"
(cd mobmek_api && dotnet tool restore >/dev/null && dotnet dotnet-ef database update --project src/MobmekApi)
ok "Schema up to date"

# --- 6. Import ---------------------------------------------------------------
step "Running the legacy importer${*:+ ($*)}"
(cd mobmek_api && dotnet run --project src/MobmekApi.LegacyImport -- "$@")

step "Done"
ok "Legacy data is in Postgres (localhost:5433, db 'mobmek')."
ok "Review the legacy-import-report-*.md written above for flags + reconciliation."
