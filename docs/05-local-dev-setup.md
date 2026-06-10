# 05 — Local Dev Setup & Troubleshooting

## Prerequisites

- **.NET 10 SDK** — `dotnet --version` should be ≥ `10.0`.
- **Docker Desktop**, running — Aspire starts PostgreSQL in a container.
- (Frontend, later) **Node.js** 20+.

## First-time setup

```bash
dotnet tool restore     # restores the local dotnet-ef tool from .config/dotnet-tools.json
dotnet build
```

## Running

```bash
dotnet run --project src/LoopQuest.AppHost
```

This starts PostgreSQL, the API (which migrates + seeds on startup), and the Aspire dashboard. Use the
dashboard's links to reach each resource — ports are assigned dynamically. On the **api** resource:
`/api/loops`, `/scalar/v1` (API explorer), `/openapi/v1.json`, `/health`, `/alive`.

You can also run the API alone (without the dashboard/containers) only if you supply a
`ConnectionStrings:loopquestdb` value — normally just run the AppHost.

## Tests

```bash
dotnet test                              # everything (integration tests need Docker)
dotnet test tests/LoopQuest.Domain.Tests # fast unit tests only
```

The integration test boots the whole app graph through Aspire and a real Postgres container; the first
run is slow because it pulls the image (~1 min). Subsequent runs are faster.

## EF Core migration workflow

Because Infrastructure has an `IDesignTimeDbContextFactory`, it is its own startup project for EF —
you don't need the AppHost running to create migrations.

```bash
# Add a migration
dotnet ef migrations add <Name> \
  --project src/LoopQuest.Infrastructure \
  --startup-project src/LoopQuest.Infrastructure \
  --output-dir Persistence/Migrations

# List / remove the last (if not yet applied)
dotnet ef migrations list  --project src/LoopQuest.Infrastructure --startup-project src/LoopQuest.Infrastructure
dotnet ef migrations remove --project src/LoopQuest.Infrastructure --startup-project src/LoopQuest.Infrastructure
```

Migrations are **applied automatically on API startup** (`DatabaseInitializer`). The design-time factory
defaults to `Host=localhost;Port=5432;Database=loopquestdb;Username=postgres;Password=postgres`;
override with the `LOOPQUEST_DESIGN_TIME_CONNECTION` env var if needed. (This connection is only used
by `dotnet ef` tooling, not at runtime.)

## Resetting the database

The Postgres container uses a **data volume** (so data survives restarts). To wipe it and re-seed:

```bash
# Stop the AppHost, then remove the Aspire-managed Postgres volume:
docker volume ls                      # find the volume (name contains "loopquest"/"postgres")
docker volume rm <volume-name>
# Next run recreates the schema (migrations) and re-seeds the loop library.
```

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| AppHost fails immediately; "cannot connect to Docker" | Docker Desktop isn't running. Start it and retry. |
| API stuck "waiting for postgres" | Postgres container still starting/unhealthy — give it a moment; check the dashboard logs. |
| Port already in use | Another instance is running; stop it, or let Aspire pick new ports on restart. |
| `dotnet ef` "no project found" / tool missing | Run `dotnet tool restore`; run EF commands from the repo root. |
| Migration error about a missing provider | You're invoking EF with the wrong `--startup-project`; use `src/LoopQuest.Infrastructure` (see above). |
| Build warns `NU1902` on OpenTelemetry | Known moderate advisory in the template's pinned 1.14.0 packages — see below. |

## Known maintenance items (from scaffolding)

These are tracked so they don't get forgotten:

- **OpenTelemetry `NU1902` advisory** — the Aspire ServiceDefaults template pins OpenTelemetry 1.14.0,
  which has a moderate-severity advisory. Bump the OpenTelemetry packages in
  `src/LoopQuest.ServiceDefaults` when convenient and re-test. (Use the NuGet MCP to check the safe
  target version.)
- **Aspire templates 13.1.0 → newer** — a newer Aspire project-template package exists
  (`dotnet new install Aspire.ProjectTemplates@<latest>`). Not urgent; our packages are already 13.4.2.
- **Central Package Management** — ✅ adopted: every package version is pinned centrally in the root
  `Directory.Packages.props`; csproj files reference packages by name only (no inline `Version`).

## Useful conventions reminder

- All measurements in **meters** until the display edge.
- New feature? Copy the `Loops/Queries/GetLoops/` slice shape.
- Don't return EF entities from controllers — project to a DTO.
