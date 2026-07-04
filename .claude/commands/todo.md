---
description: Start working through the outstanding tasks in todo/miroboard-task.md
disable-model-invocation: true
allowed-tools: Edit, Write, Bash
---

Read `todo/miroboard-task.md` and start working through the outstanding tasks under
its headings, in order. Follow `.claude/rules/todo-autonomy.md` (already loads once
you read a file under `todo/`): proceed task by task without asking for approval,
stop only for hardlocks defined in the project CLAUDE.md files, and move each
finished task into `todo/miroboard-task-to-test.md` before starting the next task —
do not run tests yet. Once `todo/miroboard-task.md` is fully empty, run one batched
test pass covering everything in `todo/miroboard-task-to-test.md`, then move each
confirmed task into `todo/miroboard-task-done.md` (with a completion timestamp,
under today's date and the matching page heading).
