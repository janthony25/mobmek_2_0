# Mobmek Frontend

Workshop management UI for the Mobmek API. Built with **React + TypeScript + Vite** and styled with **Tailwind CSS**.

## Pages

Full create / read / update / delete is available for every entity in the API.

- **Customers** — list + CRUD. Open a customer to manage their **Cars** (make→model
  cascade) and view their **Jobs**.
- **Job Center** — list + CRUD (customer→car cascade). Open a job to manage its
  **Parts/Items**, **Labour**, **Services** and assigned **Mechanics**. Job totals are
  recomputed by the backend and refresh automatically.
- **Products**, **Services** (catalog) — list + CRUD.
- **Car Makes & Models** — manage makes; select a make to manage its models.
- **Employees**, **Titles**, **Employment Types** — list + CRUD.

### How the CRUD layer is built

- `src/components/crud/` — a schema-driven `ResourceForm` + a reusable `CrudSection`
  (table with Add/Edit/Delete, modal, delete confirmation, toasts). Most pages are a
  few lines of config on top of these.
- `src/components/forms/` — bespoke forms for the cascading cases (Cars, Jobs) that the
  generic schema form can't express; they reuse the same controls and plug into
  `CrudSection` via its `renderForm` prop.
- `src/components/ui/` — `Modal`, `Button`, `ConfirmDialog`, and a `ToastProvider`.
- API errors (including ASP.NET `ProblemDetails` validation bodies) are parsed into
  readable messages in `src/api/client.ts` and surfaced inline / via toasts.

## Project structure

```
src/
├── api/          # API client + one module per resource (customers, jobs, carMakes, carModels)
├── components/
│   ├── layout/   # AppLayout shell + Sidebar
│   └── ui/       # Reusable presentational pieces (PageHeader, StateMessage)
├── hooks/        # useAsync data-fetching hook
├── pages/        # One component per route
├── types/        # Domain types mirroring the API DTOs
├── App.tsx       # Route definitions
└── main.tsx      # App entry (router provider)
```

The `@/` import alias maps to `src/` (see `vite.config.ts` and `tsconfig.app.json`).

## Local development

```bash
npm install
npm run dev      # http://localhost:3000
```

API requests go to `/api` and are proxied to the backend. By default the dev
proxy targets `http://localhost:8080`; override with `VITE_API_TARGET`. See
`.env.example`.

## Scripts

- `npm run dev` — start the Vite dev server.
- `npm run build` — type-check and produce a production build in `dist/`.
- `npm run preview` — preview the production build.
- `npm run lint` — run oxlint.

## Docker

The project ships a multi-stage `Dockerfile` that builds the app and serves it
with nginx (which also reverse-proxies `/api` to the `api` service). It is wired
into the root `docker-compose.yml` as the `frontend` service:

```bash
# from the repo root
docker compose up --build
```

The UI is then available at http://localhost:3000.
