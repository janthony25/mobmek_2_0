# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Workshop-management UI (`mobmek_frontend`) for the **Mobmek API** — a .NET backend that
lives as a sibling directory (`../mobmek_api`) in the same git repo (`/Users/jun/mobmek_2_0`).
React 19 + TypeScript + Vite, styled with Tailwind CSS v4. There is no test suite.

## Workflow for code-change requests (do this every time)

Before writing or editing any code in response to a request:

- **Clarify first — ask, don't assume.** Ask the questions needed to be sure you understand
  exactly what's wanted (which pages/components, which form fields and whether each is required
  vs. optional, which API endpoints/DTOs are involved, the cascade/relationship between fields,
  validation rules, loading/empty/error states, and naming). Do not start coding until genuine
  ambiguity is resolved. A request that seems clear can still hide assumptions — surface them.
- **Challenge what looks wrong.** If something in the request seems mistaken, inconsistent with
  what's already here, or likely not what the user actually wants (e.g. a field marked required
  that's optional in the DTO/elsewhere, an odd name, a cascade that doesn't fit, a free-typed
  value where an id from a lookup is expected), stop and ask whether they really want it before
  proceeding. Be vigilant — flag it even when you're only somewhat unsure. The user would rather
  be asked than get the wrong thing.
- **Lay out a to-do list before changing anything.** Present the concrete, ordered steps you're
  about to take (files/components to add or edit, type changes in `src/types`, API modules,
  routes) so the user can see exactly what will change, then work through them one by one and
  keep the list visible/updated as you go.
- **Verify before calling it done.** There's no test suite, so `npx tsc -b` (type check) and
  `npm run lint` (oxlint) passing is the mandatory gate for every change — it's cheap, always run it.
  Live browser verification (dev server + click through, screenshots) is **not required for every
  change** — reserve it for new components, layout/structural rework, or new interactive behavior
  (a new button/action, a new form, a cascading field). Skip it for copy/className/formatting
  tweaks, renames, or anything the type checker already proves correct. When it is warranted,
  do the smallest check that proves it (one interaction, one screenshot) rather than a full
  multi-step session, and don't spin up a research subagent just to verify a UI change — read the
  file directly. If you skip visual verification, say so explicitly rather than implying it was
  tested.

The goal: build exactly the right feature, correctly — never silently guess.

## Tech stack (keep it lean)

Runtime dependencies are deliberately minimal — **`react`, `react-dom`, `react-router-dom`**
only. Everything else is dev tooling: **Vite 8** (`@vitejs/plugin-react`), **TypeScript 6**,
**Tailwind CSS v4** (via `@tailwindcss/vite`), and **oxlint** for linting. There is no state
library (Redux/Zustand/React Query), no data-fetching library, no UI component library, no
form library, no icon package, no CSS-in-JS, and no date library — those needs are met by the
in-house primitives below.

### Styling
- **Tailwind CSS v4**, imported in `src/index.css` via `@import 'tailwindcss'` (v4's CSS-first
  setup — there is **no `tailwind.config.js`**; configure via CSS/`@theme` if customization is ever needed).
- Styling is **utility classes inline in JSX** — no CSS modules, no styled-components, no
  separate stylesheets beyond `index.css`. Match the existing slate-based palette
  (`bg-slate-900`, `text-slate-600`, `border-slate-300`, `red-600` for danger).
- Reusable styling lives in components, not abstractions: variant maps in `ui/Button.tsx`
  (`primary`/`secondary`/`danger`/`ghost`), and the shared input class `controlClass` in
  `forms/controls.tsx`. Reuse these rather than re-deriving class strings.

### Dependency policy — ask before adding
**Do not add a new dependency (runtime or dev) without first checking with the user.** Before
proposing one, confirm the need can't be met with the current stack: the in-house CRUD layer,
`useAsync`, the `ui/` primitives, `lib/format.ts` formatters, and plain Tailwind/React. When a
new package genuinely seems warranted, surface it explicitly — state what it's for, what it
costs (bundle size, maintenance), and the no-dependency alternative — and let the user decide.

## Commands

```bash
npm run dev      # Vite dev server on http://localhost:3000
npm run build    # tsc -b (type-check) then vite build -> dist/
npm run lint     # oxlint (NOT eslint, despite eslint-disable comments in source)
npm run preview  # serve the production build
```

The dev server proxies `/api` to the backend at `http://localhost:8080` (override with
`VITE_API_TARGET`). `npm run build` runs the type-checker first, so a type error fails the
build — treat `tsc -b` as the gate before considering a change done.

To run the full stack (frontend + api + db) use `docker compose up --build` from the repo
root (`../`), which serves this app via nginx that also reverse-proxies `/api` to the api
service.

## TypeScript constraints

`erasableSyntaxOnly` is enabled in tsconfig, which **forbids `enum`**. Backend enums are
expressed as `const` objects plus a union type, with a separate `*_LABELS` record for
display (see `JobStatus`, `MarkupSolution` in `src/types/index.ts`). Follow that pattern for
any new enum.

The `@/` import alias maps to `src/` (configured in both `vite.config.ts` and
`tsconfig.app.json` — update both if changed).

## Architecture

The app is a thin client over the REST API. Every domain entity maps to:
**`src/types/index.ts` DTO ↔ `src/api/<resource>.ts` module ↔ a page/section.**

### API layer (`src/api/`)
- `client.ts` is the only place that touches `fetch`. It exposes `apiGet/apiPost/apiPut/apiDelete`
  against a relative base (`VITE_API_BASE_URL`, default `/api`). It parses ASP.NET
  `ProblemDetails` / validation bodies into a readable `ApiError.message` and handles 204/empty
  responses. **Never call `fetch` directly elsewhere** — add resource modules on top of these helpers.
- One module per resource. Nested resources use a `base = (jobId) => \`/jobs/${jobId}/items\``
  pattern, mirroring the backend's job-aggregate routing (see `jobItems.ts`, `invoices.ts`).

### Backend domain model (important for getting routes/behaviour right)
The **Job is an aggregate root**: items, labour, service lines, and invoices are nested under
`/jobs/{jobId}/...` and child operations are scoped to both jobId and child id. Job totals
(`totalJobPrice`, `totalJobProfit`) are recomputed by the backend — after any mutation, reload
the job rather than computing client-side. Invoices are **generated** from the job (never
deleted; a `reject` endpoint sets status). GST is a singleton setting (rate as a fraction,
e.g. 0.15) snapshotted onto each invoice. See the `workshopapi-design-reference` memory for the
fuller backend design rationale.

### CRUD layer (`src/components/crud/`) — the core abstraction
Most pages are config, not code, built on two pieces:
- **`CrudSection`** — table with Add/Edit/Delete, modal, delete confirmation, and toasts. Driven
  by `load`/`onCreate`/`onUpdate`/`onDelete` callbacks plus `columns`. Bump `reloadKey` to force a
  reload when a parent scope changes; `onChanged` fires after mutations to refresh a parent.
- **`ResourceForm`** — renders a form from a declarative `FieldSchema[]` (`crud/types.ts`).

Two ways to supply the form to a `CrudSection`:
1. **Schema form** — pass `fields`. Used for simple entities. `LookupCrudPage` wraps this for the
   many "name only" lookup entities (titles, employment types, car makes).
2. **`renderForm` prop** — a bespoke form component, for cascading cases the schema can't express
   (customer→car make→model, customer→car in `forms/CarForm.tsx`, `forms/JobForm.tsx`). These reuse
   the shared `Field`/`controlClass` from `forms/controls.tsx`.

When adding an entity, prefer the schema/`LookupCrudPage` path; only reach for `renderForm` when
fields depend on each other.

### Pages & routing (`src/App.tsx`, `src/pages/`)
Routes are flat under a single `AppLayout` (sidebar shell). Detail pages embed `CrudSection`s with
`variant="section"` for nested resources (e.g. `JobDetailPage` manages items/labour/services/mechanics;
`CustomerDetailPage` manages a customer's cars). Job creation has its own route (`jobs/new` →
`NewJobPage`) rather than a modal, because of the cascading selects.

### Shared pieces
- `hooks/useAsync.ts` — the data-fetching primitive (`{ data, loading, error, reload }`); `CrudSection`
  and pages use it instead of any data library. Re-runs on dependency change; `reload()` re-fetches.
- `components/ui/` — `Modal`, `Button`, `ConfirmDialog`, `StateMessage`, `PageHeader`, and `ToastProvider`/`useToast`.
- `lib/format.ts` — `currency`, `date`, `percent`, `orDash` formatters (dashes for null/empty). Use these for all display formatting.
