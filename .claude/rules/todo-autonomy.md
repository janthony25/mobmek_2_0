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
- **Move a task to the done file before moving to the next task**, not after. If work
  is interrupted (e.g. running out of context), `miroboard-task.md` must accurately
  reflect only what's actually still outstanding. See below for how the move works.

## Completing a task: `miroboard-task.md` → `miroboard-task-done.md`

`todo/miroboard-task.md` holds outstanding tasks; `todo/miroboard-task-done.md` holds
finished ones. The two files must always share the same heading structure (`#`, `##`,
...) — never require the user to add a heading to both files manually.

When a task is finished:

1. Remove its checkbox line from `todo/miroboard-task.md`.
2. Add that line under the matching heading in `todo/miroboard-task-done.md`. If the
   heading — or any of its ancestor headings (e.g. `## Filters` under `# Customer
   Page`) — doesn't exist there yet, create the full missing chain (mirroring the
   exact heading levels and text from `miroboard-task.md`) rather than asking the
   user to add it.
3. Append the completion date and time to the moved line, e.g.
   `- [x] Fix cascading car-make select (done: 2026-07-04 14:32)`. Use the current
   date/time at the moment the task is completed.
4. Leave headings in `miroboard-task.md` in place even if every task under them has
   been moved out — new tasks may be added under that heading later, so don't delete
   the heading just because it's temporarily empty.