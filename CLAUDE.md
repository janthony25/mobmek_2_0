# CLAUDE.md

This file provides guidance to Claude Code when working anywhere in this repository (`mobmek_2_0`), including cross-cutting areas that span both sub-projects or sit outside them (infrastructure, deploy scripts, legacy import, docker-compose, business-level docs). For backend-specific or frontend-specific guidance, see `mobmek_api/CLAUDE.md` and `mobmek_frontend/CLAUDE.md` — both also apply when you're inside those directories.

## What this is

A workshop-management system for an NZ auto-repair shop: `mobmek_api` (.NET backend) + `mobmek_frontend` (React/TS frontend), Postgres, deployed to a single EC2 box behind Caddy/TLS.

## Rule: keep `docs/features/` up to date — every change, no exceptions

`docs/features/` holds one living, code-verified markdown file per feature area (index + cross-feature table at `docs/features/README.md`). It exists so nobody has to re-read the whole codebase to answer "what do we have, what works, what's broken, what's still just a doc."

**Any change that adds, fixes, removes, or discovers a gap in feature behavior must update the corresponding `docs/features/*.md` file in the same change.** This applies regardless of which part of the repo the change touches — backend, frontend, infra scripts, deploy config, legacy import, docs. Specifically:

- New capability shipped → mark it **Working** with file:line evidence, remove or correct any "Partial"/"Planned only" note it supersedes.
- Bug fixed → update the relevant status line; remove the gap if it's now closed.
- New gap discovered (even incidentally, while working on something else) → add it rather than letting it go unrecorded.
- Touching an area with no existing file → create one rather than leaving the change undocumented. Follow the structure of the existing files (status summary table, then per-capability detail with evidence, then a drift/follow-ups section).
- Status claims must be **code-verified** (a real file path, ideally with line/function), not inferred from a design doc, a commit message, or memory. If you can't point at the code, say "Unverified," don't say "Working."

Do not skip this because a change feels small — a stale status doc is worse than no doc, since it actively misleads the next person (or the next Claude session) who trusts it.

`docs/features/feature-gaps.md` is the consolidated backlog of every open gap across all the files above. When a change closes a gap, check it off (or delete the line) there too, in the same change. When a change surfaces a new gap, add it there as well — not just in the per-feature file.

See the `feature-status-docs` memory entry for how this was bootstrapped (2026-10-07 full-codebase audit) and what it corrected.

## Rule: log every commit in `CHANGELOG.md`

`CHANGELOG.md` (repo root) is a running, human-readable log of what shipped and why — reverse-chronological, grouped by date heading (`## YYYY-MM-DD`), newest date and newest entry within a date both at the top.

**Every commit must add its own entry to `CHANGELOG.md` in that same commit.** This applies to every commit in this repo — backend, frontend, infra, scripts, docs-only — not just user-facing features.

- If there's no heading for today yet, add one at the very top of the file.
- Write one bullet (two lines only if genuinely needed): a short bold summary of what changed, then — after an em dash — the reason/impact when that's the useful part (a bug's user-visible symptom, a decision's rationale). Don't just restate the diff; match the tone of the existing entries (go read a few before writing one).
- You can't know a commit's own hash before it exists, so the trailing `(`short-hash`)` is optional for the commit introducing the entry — append it later (a small follow-up edit, not an amend) if it's easy to, but don't block the commit on it.
- Skip pure reverts/merges with no independent content, and skip amend-in-place fixups to a commit not yet pushed (fold the changelog entry into the one it's fixing instead of stacking a second line).

Treat a commit with no changelog entry as incomplete, the same way a commit that skips the `docs/features/` update above is incomplete.
