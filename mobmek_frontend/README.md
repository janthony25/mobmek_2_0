# Mobmek Frontend

Workshop management UI for the Mobmek API. Built with **React + TypeScript + Vite** and styled with **Tailwind CSS**.

## Pages

- **Customers** — table of all customers.
- **Job Center** — cards for every job with status, vehicle and totals.
- **Car Makes & Models** — pick a make on the left to load its models on the right.

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
