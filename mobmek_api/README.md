# Mobmek API

ASP.NET Core Web API built on **.NET 10**, using **PostgreSQL** via **Entity Framework Core**, documented with **Swagger**, and tested with **xUnit**.

## Tech stack

| Concern        | Choice                                   |
| -------------- | ---------------------------------------- |
| Framework      | .NET 10 / ASP.NET Core (controllers)     |
| Database       | PostgreSQL                               |
| ORM            | EF Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`) |
| API docs       | Swagger UI (`Swashbuckle.AspNetCore`)    |
| Tests          | xUnit + EF Core InMemory provider        |

## Project layout

```
mobmek_api.slnx                # solution
src/MobmekApi/
├── Controllers/               # HTTP endpoints (thin)
├── Services/                  # Business logic (IProductService / ProductService)
├── Data/                      # AppDbContext (EF Core)
├── Entities/                  # Persisted domain models
├── DTOs/                      # Request/response contracts
├── Migrations/                # EF Core migrations
└── Program.cs                 # Composition root / pipeline
tests/MobmekApi.Tests/         # xUnit test project
```

The included `Product` entity is a sample slice (entity → DTO → service → controller → tests).
Replace it with the real Mobmek domain model.

## Quick start

> All `docker compose` commands run from the **repo root** (`mobmek_2_0/`), where `docker-compose.yml` lives.

### Every day (engine already running)

```bash
cd /Users/jun/mobmek_2_0
docker compose up -d            # start Postgres + API in the background
```

Open **Swagger UI → http://localhost:8080/swagger**

Stop when you're done:

```bash
docker compose down            # stops containers; your data is kept in a volume
```

### After a laptop restart (do this first!)

The Docker engine on this Mac is **colima**, and it does **not** start automatically after a reboot.
If `docker compose up` fails with something like *"Cannot connect to the Docker daemon"*, start colima first:

```bash
colima start                   # boots the Linux VM that runs Docker (~30s)
docker compose up -d           # then start the project as usual
```

Check it's running anytime with `colima status`.

**Tip — make it automatic:** to have colima start at login (so you can skip the manual step), run once:

```bash
brew services start colima
```

### First time on a brand-new machine

1. Install the tools:
   ```bash
   brew install colima docker docker-compose
   ```
2. Tell the docker CLI where to find the compose plugin — add this to `~/.docker/config.json`:
   ```json
   { "cliPluginsExtraDirs": ["/opt/homebrew/lib/docker/cli-plugins"] }
   ```
3. Start the engine and the project:
   ```bash
   colima start
   cd /Users/jun/mobmek_2_0
   docker compose up -d --build     # --build needed the first time
   ```

## Prerequisites

- .NET 10 SDK (for building/testing locally)
- A Docker runtime (Docker Desktop or colima) for the recommended workflow, **or** a local PostgreSQL.

### Recommended: run everything with Docker Compose

A `docker-compose.yml` at the **repo root** (`mobmek_2_0/`) defines Postgres + the API.
In Development the API auto-applies EF Core migrations on startup, so no manual DB setup is needed.

```bash
# from the mobmek_2_0/ root
docker compose up -d --build      # build + start db and api
docker compose logs -f api        # follow API logs
docker compose down               # stop (add -v to also wipe the db volume)
```

Then open **Swagger UI at http://localhost:8080/swagger**.
Postgres is also published on `localhost:5432`.

### Alternative: run the API on the host

Start only the database in Docker (or use a local Postgres), then run the API directly:

```bash
docker compose up -d db
dotnet run --project src/MobmekApi        # Swagger at https://localhost:7122/swagger
```

The connection string lives in `src/MobmekApi/appsettings.json` under `ConnectionStrings:DefaultConnection`.
For local secrets, prefer `dotnet user-secrets` over committing credentials.

## Common commands

> **Note:** the .NET 10 SDK is installed in `~/.dotnet` (the system `dotnet` on this Mac is still .NET 8).
> For the `dotnet` commands below, make sure .NET 10 is first on your PATH — add this to `~/.zshrc`:
> ```bash
> export DOTNET_ROOT="$HOME/.dotnet"
> export PATH="$HOME/.dotnet:$PATH"
> ```
> (Not needed for the Docker Compose workflow — that builds the API inside the container.)

```bash
# Restore & build
dotnet build

# Run tests
dotnet test

# EF Core migrations (run from mobmek_api/)
dotnet dotnet-ef migrations add <Name> --project src/MobmekApi
dotnet dotnet-ef database update --project src/MobmekApi
```

## Running the tests

Unit tests live in `tests/MobmekApi.Tests/`. They cover the service/CRUD logic using EF Core's
**in-memory** provider, so they need **no database, no Docker, and no colima** — just the .NET 10 SDK.

```bash
cd /Users/jun/mobmek_2_0/mobmek_api    # the folder with mobmek_api.slnx
dotnet test
```

A passing run looks like:

```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: ~0.3s
```

> If `dotnet test` errors about the framework/SDK version, .NET 10 isn't on your PATH — see the
> PATH note under [Common commands](#common-commands) above.

Useful variations:

```bash
dotnet test -v n                                   # list each test name + pass/fail
dotnet test --filter "FullyQualifiedName~Delete"   # run only tests matching a name
dotnet watch test --project tests/MobmekApi.Tests  # auto-rerun tests on every file save
```

Run this before pushing changes — it's the fast way to confirm nothing broke without
clicking through Swagger by hand.

## API endpoints

| Method | Route                | Description        |
| ------ | -------------------- | ------------------ |
| GET    | `/api/products`      | List products      |
| GET    | `/api/products/{id}` | Get product by id  |
| POST   | `/api/products`      | Create product     |
| PUT    | `/api/products/{id}` | Update product     |
| DELETE | `/api/products/{id}` | Delete product     |
