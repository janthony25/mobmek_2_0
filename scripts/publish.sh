#!/usr/bin/env bash
#
# Deploys the current `main` branch to the EC2 box: pulls the latest commit there and
# rebuilds the api/frontend containers. This is the exact sequence that was previously run
# by hand over SSH — wrapped here so it can't be done with an uncommitted/unpushed tree or
# with a forgotten `--build`.
#
# Deliberately does NOT touch the db or run migrations. A schema change is a separate,
# reviewed-by-hand step — see infrastructure/docs/phase-1-plan.md, "Running migrations in
# production" — never something an automatic script should run unattended.
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

step "Deploying to $HOST"
ssh -i "$KEY" "$HOST" "cd $REMOTE_DIR && git pull origin main && docker compose up -d --build api frontend"

step "Verifying the site is back up"
sleep 2
CODE=$(curl -s -o /dev/null -w '%{http_code}' "$DOMAIN/")
[[ "$CODE" == "200" ]] || fail "Site returned HTTP $CODE after deploy — check the box."
ok "$DOMAIN is responding (200)"

step "Done"
echo "    Deployed ${LOCAL:0:7} to $HOST."
echo "    If this commit includes an EF Core migration, apply it separately — see"
echo "    infrastructure/docs/phase-1-plan.md: \"Running migrations in production\"."
