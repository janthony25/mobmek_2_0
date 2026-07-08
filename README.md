# Mobmek 2.0

Workshop management system — ASP.NET Core 10 API + React 19 frontend + PostgreSQL 17, orchestrated with Docker Compose.

## Stack

| Layer    | Technology                                    |
|----------|-----------------------------------------------|
| Frontend | React 19 · TypeScript 6 · Vite 8 · Tailwind v4 |
| Backend  | ASP.NET Core 10 · EF Core 10 · Swagger UI    |
| Database | PostgreSQL 17                                 |
| Runtime  | Docker + Docker Compose                       |

## Prerequisites

There are two ways to run Mobmek: **Docker Compose** (recommended, fewest installs) or **locally** (each service on your machine). Pick one.

### Option A — Docker Compose (recommended)

You only need Docker. Everything else (Node, .NET, Postgres) runs inside containers.

| Tool | Minimum version |
|------|----------------|
| Docker + Docker Compose | Docker 24 / Compose v2 |

#### Install Docker on macOS

**Recommended — Docker Desktop (simplest):**

1. Download and install [Docker Desktop for Mac](https://www.docker.com/products/docker-desktop/).
2. Open Docker Desktop and wait for the whale icon in the menu bar to turn solid.
3. Verify:
   ```bash
   docker --version
   docker compose version
   ```

**Alternative — Colima (lightweight, no GUI):**

```bash
brew install colima docker docker-compose
colima start
```

To start Colima automatically on login:
```bash
brew services start colima
```

After a reboot, if `docker compose` fails with *"Cannot connect to the Docker daemon"*, run `colima start` first.

#### Install Docker on Windows (WSL2)

1. Download and install [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/).
2. In Docker Desktop → Settings → Resources → WSL Integration, enable integration for your WSL2 distro.
3. Open your WSL2 terminal and verify:
   ```bash
   docker --version
   docker compose version
   ```

#### Install Docker on Linux

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker $USER   # allows running docker without sudo (re-login after this)
```

---

### Option B — Local development

If you want to run each service directly on your machine (e.g. to use `dotnet watch` or `npm run dev` with hot-reload), you need:

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0 | Build and run the API |
| Node.js | 18+ | Run the frontend dev server |
| npm | 9+ | Manage frontend packages |
| PostgreSQL | 17 | Database (or run just the DB via Docker) |

#### Install .NET 10 SDK on macOS

```bash
# Using the official installer script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0

# Add to your shell profile (~/.zshrc or ~/.bash_profile)
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"

# Reload your shell, then verify
dotnet --version   # should print 10.x.x
```

Alternatively, download the .pkg installer from [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).

#### Install .NET 10 SDK on Windows (WSL2) / Linux

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0

# Add to ~/.bashrc or ~/.zshrc
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"

source ~/.bashrc   # or source ~/.zshrc

dotnet --version   # should print 10.x.x
```

#### Install Node.js

**macOS:**
```bash
brew install node
```

**Linux / WSL2:**
```bash
curl -fsSL https://deb.nodesource.com/setup_lts.x | sudo -E bash -
sudo apt-get install -y nodejs
```

**Verify:**
```bash
node --version   # 18+
npm --version    # 9+
```

---

## Running the project

### With Docker Compose (Option A)

Run all three services (frontend, API, database) from the repo root:

```bash
cd mobmek_2_0

# First time: copy the env template and fill in real values before deploying
# anywhere beyond a throwaway local machine (the defaults are dev-only).
cp .env.example .env

# First time, or after changing the Dockerfile
docker compose up -d --build

# Every day after that
docker compose up -d
```

`.env` (gitignored) holds the DB password and the bootstrap admin login — see
`docs/auth-module-design.md` and `.env.example` for what each variable does. Sign
in to the app with the `BOOTSTRAP_ADMIN_EMAIL` / `BOOTSTRAP_ADMIN_PASSWORD` from
your `.env` (defaults: `justforvalo25@gmail.com` / `DevAdmin!2026`).

| Service  | URL |
|----------|-----|
| Frontend | http://localhost:3000 |
| API (Swagger) | http://localhost:8080/swagger |
| PostgreSQL | localhost:5433 |

Stop the stack:
```bash
docker compose down           # stops containers, keeps data
docker compose down -v        # stops containers AND wipes the database volume
```

View logs:
```bash
docker compose logs -f          # all services
docker compose logs -f api      # API only
docker compose logs -f frontend # frontend only
```

---

### Local development (Option B)

Start only the database in Docker, then run the API and frontend on your machine for fast hot-reload.

**1. Start the database:**
```bash
cd mobmek_2_0
docker compose up -d db
```

**2. Run the API:**
```bash
cd mobmek_2_0/mobmek_api
dotnet run --project src/MobmekApi
```
Swagger UI: https://localhost:7122/swagger

**3. Run the frontend (in a separate terminal):**
```bash
cd mobmek_2_0/mobmek_frontend
npm install        # first time only
npm run dev
```
Frontend: http://localhost:3000 (proxies `/api` → `http://localhost:8080` by default)

---

## Project structure

```
mobmek_2_0/
├── docker-compose.yml       # Orchestrates db + api + frontend
├── mobmek_api/              # ASP.NET Core 10 Web API
│   ├── src/MobmekApi/       # Application source
│   │   ├── Controllers/     # HTTP endpoints (thin)
│   │   ├── Services/        # Business logic
│   │   ├── Entities/        # Domain models (inherit BaseEntity)
│   │   ├── DTOs/            # Request/response contracts
│   │   ├── Data/            # AppDbContext (EF Core)
│   │   └── Migrations/      # EF Core migrations
│   └── tests/               # xUnit tests (in-memory DB, no Docker needed)
└── mobmek_frontend/         # React 19 + Vite SPA
    └── src/
        ├── api/             # API client + one module per resource
        ├── components/      # Shared UI + CRUD layer
        ├── pages/           # One component per route
        └── types/           # TypeScript DTOs mirroring the API
```

## Running API tests

Tests use an in-memory database — no Docker or PostgreSQL needed, just the .NET 10 SDK.

```bash
cd mobmek_2_0/mobmek_api
dotnet test                                          # run all tests
dotnet test -v n                                     # list test names
dotnet test --filter "FullyQualifiedName~Delete"     # run a subset by name
dotnet watch test --project tests/MobmekApi.Tests    # auto-rerun on save
```

## Ports reference

| Port | Service |
|------|---------|
| 3000 | Frontend (nginx in Docker / Vite dev server locally) |
| 8080 | API (Docker container) |
| 7122 | API (local `dotnet run`, HTTPS) |
| 5433 | PostgreSQL (host-side port; container internal is 5432) |
