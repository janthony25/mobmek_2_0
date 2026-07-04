---
paths:
  - "todo/**"
---

# Working through `todo/` task lists

Files under `todo/` are pre-agreed task lists (checkboxes). The ambiguity-resolution
workflow in `mobmek_api/CLAUDE.md` / `mobmek_frontend/CLAUDE.md` ("clarify first",
"lay out a to-do list before changing anything") is considered already satisfied by
the existence of the todo file — do not re-ask for permission before each task.

**Headings (`#`, `##`, ...) are page/section labels, not tasks.** A heading like
`# Customer Page` scopes every checkbox listed under it to that page/section — treat
those tasks as work to be done in the Customer Page, not as generic standalone items.
Use the nearest heading above a checkbox to determine which page/section/feature it
belongs to.

- **Proceed through tasks without stopping for approval.** Work task by task without
  waiting for a go-ahead between them.
- **Hardlocks still apply — stop and ask.** Hardlocks are enforced as `ask` rules in
  `.claude/settings.local.json` (not just written guidance), so the permission prompt
  itself is the "stop and ask" — don't try to work around it. Covers: adding/removing
  a dependency (`npm install`/`npm i`/`npm uninstall`, `yarn add`/`remove`, `pnpm
  add`/`remove`, `dotnet add package`/`remove package` — see also the frontend's "ask
  before adding a dependency" policy in its CLAUDE.md) and destructive ops
  (`git push --force`, `rm -rf`, `git reset --hard`, `docker system prune`, dropping
  the database or removing a migration). When the prompt appears, pause and let the
  user decide rather than finding another way to accomplish the same thing.
- **Every single `- [ ]` line is its own atomic task, even when several sit under
  the same heading and touch the same page or file.** "Related" or "same page" is
  not a reason to treat them as one unit. Each `- [ ]` gets its own implement → move
  cycle, full stop.
- **Move each task to the to-test file the instant its implementation finishes — one
  at a time, never batched.** The mechanical loop is:
  1. Look at `miroboard-task.md` and pick the single next unimplemented `- [ ]` line.
  2. Implement only that one task. Do not touch the code for any other checkbox yet,
     even one immediately below it under the same heading.
  3. Before writing any code for the *next* checkbox, edit the files now: remove
     that one line from `miroboard-task.md` and add it to
     `miroboard-task-to-test.md`.
  4. Only after that file edit is done, go back to step 1 for the next checkbox.

  **Anti-pattern — do not do this:** implementing checkbox A, then checkbox B, then
  checkbox C (because they're all on the same page and it's more efficient to code
  them in one pass), and only then editing `miroboard-task.md`/`miroboard-task-to-
  test.md` once to move all three. This has happened before and is exactly the
  behavior this rule forbids — the file edit for task A must land before any code
  is written for task B, regardless of how related or efficient-to-batch they seem.
  If work is interrupted (e.g. running out of context), `miroboard-task.md` must
  accurately reflect only what's actually still outstanding — which is only
  possible if moves happen one at a time as-you-go, not in a batch at the end.
- **Do not run tests after each individual task.** Testing is deliberately batched
  (see below) so that pages touched by several tasks in the same session only get
  tested once, instead of once per task — this is what keeps token/tool cost down
  when a session covers multiple changes to the same page.

## Stage 1 — Implement: `miroboard-task.md` → `miroboard-task-to-test.md`

`todo/miroboard-task.md` holds outstanding (not-yet-implemented) tasks, grouped only
by page/section heading. `todo/miroboard-task-to-test.md` holds tasks whose code is
implemented but not yet tested, mirroring the **same page/section heading structure**
as `miroboard-task.md` (no date layer here — the date is only recorded once a task
reaches `miroboard-task-done.md`). Never require the user to add a heading manually —
create whatever heading is missing when moving a task.

When a task's implementation is finished:

1. Remove its checkbox line from `todo/miroboard-task.md` **immediately** — do not
   leave it there "pending batch" until sibling tasks under the same heading are
   also implemented.
2. In `todo/miroboard-task-to-test.md`, find or create the matching page/section
   heading, mirroring the exact heading text/level from `miroboard-task.md`.
3. Add the task line under that heading as-is (no timestamp yet — the timestamp is
   added at Stage 2 once it's actually tested and confirmed working).
4. Leave headings in `miroboard-task.md` in place even if every task under them has
   been moved out — new tasks may be added under that heading later, so don't delete
   the heading just because it's temporarily empty.
5. Do **not** run any tests yet. Continue straight to the next outstanding task in
   `miroboard-task.md`.

## Stage 2 — Batch test: `miroboard-task-to-test.md` → `miroboard-task-done.md`

Only **once `miroboard-task.md` has zero outstanding tasks left** (every task has
been implemented and moved to `miroboard-task-to-test.md`), run one test pass
covering everything accumulated there:

1. Read through all of `miroboard-task-to-test.md` and identify every page/area
   touched across all its tasks.
2. Run the test suites and/or manual verification (e.g. the `/verify` skill,
   `dotnet test` for backend changes, the frontend test runner for frontend changes)
   relevant to those pages/areas **once, together** — not once per task and not once
   per page.
3. If a problem surfaces that traces back to a specific task, fix it before moving
   that task on. A task only moves to `miroboard-task-done.md` once it's confirmed
   working.
4. For each confirmed task, move it into `miroboard-task-done.md`: find or create
   today's date heading as a top-level heading (`# 2026-07-04`, placed after any
   earlier dates), then find or create the matching page/section heading one level
   deeper (`## Customer Page`), mirroring the exact heading text from
   `miroboard-task-to-test.md`/`miroboard-task.md`. If the page heading has its own
   sub-headings (e.g. `### Filters`), mirror those too, nested one level deeper.
5. Append the completion date and time to the moved line, e.g.
   `- [x] Fix cascading car-make select (done: 2026-07-04 14:32)`. Use the current
   date/time at the moment the task is confirmed, not when it was implemented.
6. Remove the line from `miroboard-task-to-test.md` once it's moved to done.

**Resuming mid-pipeline:** if a session starts and `miroboard-task.md` is already
empty while `miroboard-task-to-test.md` still has entries left over from a prior,
interrupted session, run Stage 2 on those leftovers first before doing anything
else — don't leave implemented-but-untested tasks sitting there while picking up
unrelated new work.