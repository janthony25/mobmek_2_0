#!/usr/bin/env bash
#
# Generates the production migration step (infrastructure/docs/phase-1-plan.md).
#
# The deployed box's API container is the ASP.NET *runtime* image only — no SDK, no
# dotnet-ef — and Program.cs deliberately only auto-migrates in Development. So applying
# a schema change in production means generating the SQL here, where the SDK already is,
# and running it on the box with the `psql` already built into the postgres:17 image.
#
# --idempotent wraps every migration in a guard against __EFMigrationsHistory, so the
# output is always "every migration from the start of time, skip what's already applied" —
# safe to run against a database on any migration, not just the one you tested against.
#
# Usage:
#   ./scripts/generate-migration-script.sh
#
# Then, on the target box:
#   scp migrate.sql <box>:~/
#   ssh <box>
#   docker exec mobmek_db pg_dump -U postgres mobmek | gzip > "pre-migrate-$(date +%Y%m%d%H%M).sql.gz"   # safety net
#   docker compose exec -T db psql -U postgres -d mobmek -v ON_ERROR_STOP=1 < migrate.sql

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT/mobmek_api"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

# The .NET 10 SDK lives in ~/.dotnet; the system `dotnet` is .NET 8 (see mobmek_api/CLAUDE.md).
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

step "Checking for the .NET 10 SDK"
command -v dotnet >/dev/null 2>&1 || fail "dotnet not found. Run ./dotnet-install.sh first."
dotnet --list-sdks | grep -q '^10\.' || fail "No .NET 10 SDK found at $DOTNET_ROOT. Run ./dotnet-install.sh."
ok "Found $(dotnet --list-sdks | grep '^10\.' | head -1)"

OUT="$REPO_ROOT/migrate.sql"
step "Generating idempotent migration script -> $OUT"
dotnet dotnet-ef migrations script --idempotent --project src/MobmekApi -o "$OUT"
ok "Wrote $(wc -l < "$OUT" | tr -d ' ') lines"

step "Review before you ship it"
echo "    This script is derived straight from the migrations already committed to this"
echo "    repo — nothing here should surprise you. Still, open it and skim for anything"
echo "    that drops a column or renames data, not just additive changes:"
echo
echo "      less $OUT"
echo
echo "    Then deploy it (see this script's header for the exact commands) — take a"
echo "    pg_dump snapshot on the box first. An additive migration (new table, new"
echo "    nullable column) is low-risk; anything that drops or rewrites data should wait"
echo "    for a tested restore drill before it runs anywhere real."
