# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ASP.NET Core Web API on **.NET 10**, PostgreSQL via **EF Core 10** (Npgsql), Swagger docs, xUnit tests. The `Product` slice (entity → DTO → service → controller → tests) is sample scaffolding meant to be replaced with the real Mobmek domain.

## Workflow for code-change requests (do this every time)

Before writing or editing any code in response to a request:

1. **Clarify first — ask, don't assume.** Ask the questions needed to be sure you understand exactly what's wanted (which entities/fields, required vs. optional, relationships, endpoints, edge cases, naming). Do not start coding until genuine ambiguity is resolved. A request that seems clear can still hide assumptions — surface them.
2. **Challenge what looks wrong.** If something in the request seems mistaken, inconsistent with what's already here, or likely not what the user actually wants (e.g. a field marked required that's optional elsewhere, an odd name, a relationship that doesn't fit), **stop and ask whether they really want it** before proceeding. Be vigilant about this — flag it even when you're only somewhat unsure. The user would rather be asked than get the wrong thing.
3. **Lay out a to-do list before changing anything.** Present the concrete, ordered steps you're about to take (files to add/edit, migration, tests) so the user can see exactly what will change, then work through them one by one and keep the list visible/updated as you go.

The goal: build exactly the right feature, correctly — never silently guess.

## PATH gotcha (read first)

The .NET 10 SDK lives in `~/.dotnet`; the system `dotnet` on this Mac is .NET 8. Any `dotnet` command below requires .NET 10 first on PATH:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

If a `dotnet` command errors about framework/SDK version, this is why. (Not needed for the Docker Compose workflow — it builds inside the container.)

## Commands

Run from this directory (`mobmek_api/`, the folder with `mobmek_api.slnx`):

```bash
dotnet build
dotnet test                                        # all tests
dotnet test --filter "FullyQualifiedName~Delete"   # single test / subset by name
dotnet test -v n                                   # list each test name
dotnet watch test --project tests/MobmekApi.Tests  # rerun on save

# EF Core migrations (dotnet-ef is a local tool, hence `dotnet dotnet-ef`)
dotnet dotnet-ef migrations add <Name> --project src/MobmekApi
dotnet dotnet-ef database update --project src/MobmekApi
```

Tests use EF Core's **in-memory** provider — no database, Docker, or colima needed.

### Testing rule (do this for every API change)

Whenever you add or change API behavior — a new endpoint, a new/changed service method, or any business logic — **add or update the corresponding xUnit tests in `tests/MobmekApi.Tests/` in the same change.** The goal is that `dotnet test` confirms behavior, so nobody has to click through Swagger by hand to verify each endpoint.

- Test at the **service layer** (the `{X}Service` against an in-memory `AppDbContext`) — that's where the logic lives. See `ProductServiceTests` for the pattern.
- Cover both the happy path and the edge cases the service signals: not-found (`null`), delete miss (`false`), validation/ordering, and audit stamping (`UpdatedAtUtc`).
- Run `dotnet test` before considering the change done. A pure passthrough on a thin controller (no logic beyond status-code mapping) doesn't need its own test, but the service method behind it does.

## Running the app

`docker-compose.yml` lives at the **repo root** (`mobmek_2_0/`, the parent of this directory), not here. Run compose commands from there:

```bash
cd /Users/jun/mobmek_2_0
docker compose up -d --build    # --build only needed first time
```

Swagger: http://localhost:8080/swagger · Postgres published on `localhost:5432`.

The Docker engine is **colima** and does NOT auto-start after reboot. If compose fails to connect to the daemon, run `colima start` first.

Host-only alternative: `docker compose up -d db` then `dotnet run --project src/MobmekApi` (Swagger at https://localhost:7122/swagger).

## Architecture

Strictly layered, one responsibility per layer:

- **Controllers/** — thin HTTP adapters. Translate service results to status codes (e.g. `null` → 404, `bool` delete → 204/404) and nothing more. No business logic.
- **Services/** — all business logic and EF Core access, behind an interface (`IProductService` / `ProductService`), registered scoped in `Program.cs`. Services map entities ↔ DTOs; entities never leave this layer.
- **DTOs/** — request/response records; the only types controllers expose.
- **Entities/** — persisted models. All inherit **`BaseEntity`** (`Id` Guid, `CreatedAtUtc`, `UpdatedAtUtc`).
- **Data/AppDbContext** — EF config in `OnModelCreating`; **`SaveChangesAsync` auto-stamps `UpdatedAtUtc`** on any modified `BaseEntity`, so services never set it manually.

`Program.cs` is the composition root. In **Development only** it auto-applies pending migrations on startup (`db.Database.Migrate()`) — production must run migrations as a deliberate separate step. It also exposes `public partial class Program` solely so integration tests can reference the entry point.

### Entity field notation

When an entity or field is given with a trailing **`?`** (e.g. `emailAddress?`, `Notes?`), it means that field is **optional/nullable**: declare it as a nullable C# type (`string?`), no `[Required]` attribute, no `.IsRequired()` in `OnModelCreating`, and it's nullable in the DTOs. Fields without `?` are required.

### Adding a new domain slice

Follow the `Product` pattern end to end: entity (inherit `BaseEntity`) → register `DbSet` + config in `AppDbContext.OnModelCreating` → DTOs → `I{X}Service`/`{X}Service` → register scoped in `Program.cs` → thin controller → service-level xUnit tests using the in-memory context → `migrations add`.

Conventions to keep: services return DTOs (or `null`/`bool`), use `AsNoTracking()` for reads, accept and thread `CancellationToken`; controllers use `[ApiController]` + route `api/[controller]` with `[ProducesResponseType]` attributes for Swagger.

## Configuration

Connection string: `ConnectionStrings:DefaultConnection` in `src/MobmekApi/appsettings.json`. For local secrets prefer `dotnet user-secrets` over committing credentials.
