# LoopQuest

> Working title — rename freely. A weekly running-challenge game built on Strava data.

LoopQuest reframes your weekly training volume as a **destination**. Each week you pick a named
"loop" (a famous trail race, an urban multi-lap, a fantasy summit) defined by two targets —
**distance** and **elevation** — and every run, hike, and walk you log that week chases it down.
Both targets must be met to conquer the loop.

The cycle: **last week chooses the challenge, this week completes it.** Your previous week's totals
calibrate the options you're offered, so the difficulty tracks your real fitness.

This repository is the **.NET 10 backend**. A React + TypeScript frontend will be added later.

---

## Status

🟢 **Stage 0 complete — walking skeleton.** The solution, clean-architecture layers, Aspire
orchestration, PostgreSQL, and one fully-working reference slice (`GET /api/loops`) are in place.
Feature work starts at Stage 1 in [docs/03-roadmap.md](docs/03-roadmap.md).

---

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 |
| API | ASP.NET Core Web API, **controllers** (not Minimal APIs) |
| Orchestration / local infra | **.NET Aspire** (13.x) |
| Architecture | Clean Architecture (Domain / Application / Infrastructure / Api) |
| Application pattern | **CQRS via MediatR** + FluentValidation pipeline behaviours |
| Data | **PostgreSQL** via EF Core (Npgsql) |
| API docs UI | OpenAPI + Scalar |
| Tests | xUnit (unit + Aspire integration) |
| External (later) | Strava REST API (OAuth 2.0) |
| Hosting target (later) | Azure App Service + managed PostgreSQL |

See the [Architecture Decision Records](docs/adr/) for the *why* behind each choice.

---

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` ≥ 10.0)
- **Docker Desktop**, running — Aspire starts PostgreSQL in a container for you
- (Later, for the frontend) **Node.js** 20+

---

## Getting started

```bash
# 1. Restore the local CLI tools (currently: dotnet-ef)
dotnet tool restore

# 2. Run the whole app via the Aspire AppHost.
#    This starts: a PostgreSQL container, the API (which migrates + seeds on startup),
#    and the Aspire dashboard.
dotnet run --project src/LoopQuest.AppHost
```

The Aspire dashboard opens in your browser and links to each running resource. From the **api**
resource you can reach:

| What | URL (port varies — use the dashboard links) |
|---|---|
| Loop library endpoint | `GET /api/loops` |
| Scalar API explorer | `/scalar/v1` |
| OpenAPI document | `/openapi/v1.json` |
| Health / liveness | `/health`, `/alive` |

> First run is slower: Aspire pulls the PostgreSQL container image.

---

## Solution layout

```
LoopQuest/
├─ src/
│  ├─ LoopQuest.Domain/          # Entities, enums, pure game rules. No dependencies.
│  ├─ LoopQuest.Application/     # CQRS use cases (MediatR), DTOs, interfaces, behaviours.
│  ├─ LoopQuest.Infrastructure/  # EF Core DbContext, configs, seed, migrations, Strava client (later).
│  ├─ LoopQuest.Api/             # Controllers, Program.cs composition root, OpenAPI/Scalar.
│  ├─ LoopQuest.AppHost/         # Aspire orchestrator — the project you run.
│  └─ LoopQuest.ServiceDefaults/ # Shared Aspire wiring (telemetry, health, resilience).
├─ tests/
│  ├─ LoopQuest.Domain.Tests/        # Fast unit tests (xUnit).
│  └─ LoopQuest.Api.IntegrationTests/# End-to-end via the Aspire AppHost (needs Docker).
└─ docs/                         # Vision, architecture, domain model, roadmap, ADRs.
```

The dependency rule points **inward**: `Api → Infrastructure → Application → Domain`. The Domain
references nothing; the Application depends only on the Domain (plus EF Core abstractions); the
outer layers depend on the inner ones, never the reverse.

---

## Common commands

```bash
# Build everything
dotnet build

# Run all tests (integration tests need Docker running)
dotnet test

# Run only the fast unit tests
dotnet test tests/LoopQuest.Domain.Tests

# Add a new EF Core migration (Infrastructure is its own startup via the design-time factory)
dotnet ef migrations add <Name> \
  --project src/LoopQuest.Infrastructure \
  --startup-project src/LoopQuest.Infrastructure \
  --output-dir Persistence/Migrations
```

---

## The reference slice

`GET /api/loops` is implemented end-to-end as the pattern to copy for every new feature:

```
HTTP  →  LoopsController        (src/LoopQuest.Api/Controllers/LoopsController.cs)
      →  GetLoopsQuery + Handler(src/LoopQuest.Application/Loops/Queries/GetLoops/)
      →  IAppDbContext          (abstraction in Application, implemented by Infrastructure)
      →  EF Core / PostgreSQL
```

Your first coding task (Stage 1) is the game's signature logic — the elevation-substitution
calculation — driven by the skipped tests in
`tests/LoopQuest.Domain.Tests/ChallengeCalculatorTests.cs`. See
[docs/03-roadmap.md](docs/03-roadmap.md).

---

## Licensing notes (deliberate choices)

- **MediatR is pinned to `12.5.0`** — v13+ is under a commercial licence. 12.x is free/open-source
  with an identical API. See [ADR-0004](docs/adr/0004-mediatr-version-and-licensing.md).
- **No FluentAssertions** — its v8 is commercially licensed. Tests use plain xUnit `Assert`
  (Shouldly is a free alternative if you want fluent syntax later).

---

## Documentation

| Doc | What it covers |
|---|---|
| [docs/00-vision-and-scope.md](docs/00-vision-and-scope.md) | What we're building and the v1 scope boundary |
| [docs/01-architecture.md](docs/01-architecture.md) | Layers, the dependency rule, request flow, conventions |
| [docs/02-domain-model.md](docs/02-domain-model.md) | Entities and the **authoritative** game rules (week, substitution, calibration) |
| [docs/03-roadmap.md](docs/03-roadmap.md) | **The staged plan** — your task list, stage by stage |
| [docs/04-strava-integration.md](docs/04-strava-integration.md) | OAuth, sync, and the public/private data boundary |
| [docs/05-local-dev-setup.md](docs/05-local-dev-setup.md) | Environment, troubleshooting, EF workflow |
| [docs/adr/](docs/adr/) | Architecture Decision Records |
