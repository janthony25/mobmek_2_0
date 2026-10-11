#!/usr/bin/env bash
#
# Deploys the current `main` branch to the EC2 box: pulls the latest commit there and
# rebuilds the api/frontend containers. This is the exact sequence that was previously run
# by hand over SSH — wrapped here so it can't be done with an uncommitted/unpushed tree or
# with a forgotten `--build`.
#
# Deliberately does NOT run migrations itself — a schema change is a separate, reviewed-
# by-hand step (see infrastructure/docs/phase-1-plan.md, "Running migrations in production"),
# never something an automatic script should run unattended. It DOES check, after pulling,
# whether the migrations this commit expects are actually present in the box's own
# `__EFMigrationsHistory` — added 2026-10-11 after a real outage where code shipped ahead
# of a migration nobody ran, and every invoice/quote generation 500'd until someone noticed.
#
# Usage: ./scripts/publish.sh

set -euo pipefail

HOST="ubuntu@3.102.246.171"
KEY="$HOME/.ssh/mobmek-prod.pem"
REMOTE_DIR="mobmek_2_0"
DOMAIN="https://workshop.mobmekauto.co.nz"

step()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[0;32m    %s\033[0m\n' "$*"; }
fail()  { printf '\033[0;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

step "Checking local repo state"
[[ "$(git rev-parse --abbrev-ref HEAD)" == "main" ]] || fail "Not on main — check out main before publishing."
[[ -z "$(git status --porcelain)" ]] || fail "Working tree has uncommitted changes — commit or stash first."
git fetch origin main >/dev/null
LOCAL=$(git rev-parse main)
REMOTE=$(git rev-parse origin/main)
[[ "$LOCAL" == "$REMOTE" ]] || fail "Local main ($LOCAL) differs from origin/main ($REMOTE) — push first."
ok "main matches origin/main (${LOCAL:0:7})"

step "Pulling on $HOST and checking for pending migrations"
if ! PENDING="$(ssh -i "$KEY" "$HOST" "REMOTE_DIR='$REMOTE_DIR' bash -s" <<'REMOTE_SCRIPT'
set -euo pipefail
cd "$REMOTE_DIR"
git pull origin main 1>&2

# Source of truth: every migration file in this commit's Migrations/ folder, vs. what
# the box's own __EFMigrationsHistory says is actually applied. Anything in the first
# list but not the second is drift that would silently break the newly deployed code.
CODE_MIGRATIONS=$(ls mobmek_api/src/MobmekApi/Migrations/*.cs 2>/dev/null \
  | grep -v -e 'Designer\.cs$' -e 'ModelSnapshot\.cs$' \
  | xargs -n1 basename \
  | sed 's/\.cs$//' \
  | sort)

APPLIED_MIGRATIONS=$(docker compose exec -T db psql -U postgres -d mobmek -tAc \
  'SELECT "MigrationId" FROM "__EFMigrationsHistory";' \
  | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' \
  | grep -v '^$' \
  | sort)

comm -23 <(printf '%s\n' "$CODE_MIGRATIONS") <(printf '%s\n' "$APPLIED_MIGRATIONS")
REMOTE_SCRIPT
)"; then
  fail "Could not check for pending migrations on $HOST (SSH or the remote check itself failed) — see output above."
fi

if [[ -n "$PENDING" ]]; then
  fail "Pending migration(s) on $HOST — this commit's code expects a schema the live DB doesn't have yet:
$PENDING

Run ./scripts/generate-migration-script.sh, then apply it on the box (see that script's
header for the exact commands) before deploying — see infrastructure/docs/phase-1-plan.md,
\"Running migrations in production\"."
fi
ok "No pending migrations — $HOST's schema matches this commit."

step "Deploying to $HOST"
ssh -i "$KEY" "$HOST" "cd $REMOTE_DIR && docker compose up -d --build api frontend"

step "Verifying the site is back up"
sleep 2
CODE=$(curl -s -o /dev/null -w '%{http_code}' "$DOMAIN/")
[[ "$CODE" == "200" ]] || fail "Site returned HTTP $CODE after deploy — check the box."
ok "$DOMAIN is responding (200)"

step "Done"
echo "    Deployed ${LOCAL:0:7} to $HOST."
echo "    If this commit includes an EF Core migration, apply it separately — see"
echo "    infrastructure/docs/phase-1-plan.md: \"Running migrations in production\"."
