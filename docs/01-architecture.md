# 01 — Architecture

## The shape: Clean Architecture

Four projects, dependencies pointing **inward**. An outer layer may depend on an inner one; never
the reverse.

```
┌─────────────────────────────────────────────────────────┐
│  Api  (controllers, Program.cs, OpenAPI/Scalar)           │
│   │ depends on                                            │
│   ▼                                                       │
│  Infrastructure  (EF Core, migrations, Strava client)     │
│   │ depends on                                            │
│   ▼                                                       │
│  Application  (CQRS use cases, DTOs, abstractions)        │
│   │ depends on                                            │
│   ▼                                                       │
│  Domain  (entities, enums, pure game rules)  ← no deps    │
└─────────────────────────────────────────────────────────┘
        AppHost + ServiceDefaults wrap all of this (Aspire)
```

Why: the **Domain** (the rules that make LoopQuest *LoopQuest*) shouldn't change because we swapped a
database or web framework. Pushing infrastructure to the edges keeps the core small, pure, and
trivially testable. It's also a pattern you'll meet constantly in professional .NET shops.

## What lives where

| Layer | Responsibility | Example types |
|---|---|---|
| **Domain** | Entities, enums, value objects, business invariants, pure calculations. No framework deps. | `Loop`, `LoopCategory`, `ChallengeCalculator` |
| **Application** | One class per use case (MediatR), the data/service abstractions it needs, DTOs, cross-cutting behaviours. | `GetLoopsQuery(+Handler)`, `IAppDbContext`, `LoopDto`, `ValidationBehavior` |
| **Infrastructure** | Concrete implementations of Application abstractions: EF Core context, mappings, migrations, seed, the Strava HTTP client (later), background jobs. | `AppDbContext`, `LoopConfiguration`, `LoopSeeder` |
| **Api** | HTTP surface only: thin controllers, composition root (`Program.cs`), problem-details, API docs. | `LoopsController`, `ValidationExceptionHandler` |
| **AppHost** | Aspire orchestrator: declares resources (Postgres) and projects, wires references. | `AppHost.cs` |
| **ServiceDefaults** | Shared Aspire config: OpenTelemetry, health checks, resilience, service discovery. | `Extensions.cs` |

## Request flow (the reference slice)

`GET /api/loops` is implemented end-to-end as the template to copy:

```
1. HTTP GET /api/loops
2. LoopsController.GetLoops      → sends GetLoopsQuery via MediatR ISender
3. (pipeline) LoggingBehavior    → logs + times the request
4. (pipeline) ValidationBehavior → runs validators (none for this query → passes through)
5. GetLoopsQueryHandler          → queries IAppDbContext.Loops, projects to LoopDto
6. AppDbContext (Infrastructure) → EF Core → PostgreSQL
7. List<LoopDto> bubbles back    → controller returns 200 OK
```

The controller never touches EF Core; the handler never touches HTTP. Each layer has one job.

## CQRS with MediatR

We separate **Commands** (change state) from **Queries** (read state). Each is a small request object
plus a handler, dispatched through MediatR's `ISender`. Benefits for this project:

- Every use case is one discoverable, independently testable unit.
- Cross-cutting concerns (validation, logging, later: transactions) are **pipeline behaviours** that
  wrap every request once, instead of being copy-pasted into handlers.

**Anatomy of a slice** (`Loops/Queries/GetLoops/`):

```
GetLoopsQuery.cs          // the request:  record : IRequest<TResponse>
GetLoopsQueryHandler.cs   // the behaviour: IRequestHandler<TRequest, TResponse>
LoopDto.cs                // the read model returned to the API
(GetLoopsQueryValidator.cs)  // add when the request has inputs to validate
```

Copy this folder shape for every new feature. Commands go under `.../Commands/<Name>/`.

> MediatR is pinned to **12.5.0** (free/open source). See
> [adr/0004-mediatr-version-and-licensing.md](adr/0004-mediatr-version-and-licensing.md).

## Cross-cutting concerns

- **Validation** — `ValidationBehavior` runs any registered `IValidator<TRequest>` before the handler
  and throws `ValidationException` on failure. The Api's `ValidationExceptionHandler` turns that into
  an RFC 9457 problem-details **400**. Parameterless queries simply pass through.
- **Logging** — `LoggingBehavior` logs each request name and how long it took.
- **Errors** — `AddProblemDetails()` + `UseExceptionHandler()` give consistent error bodies.

## Persistence boundary

The Application depends on **`IAppDbContext`** (which exposes just the `DbSet`s and `SaveChangesAsync`),
not on the concrete `AppDbContext`. Infrastructure implements it. This keeps PostgreSQL knowledge out
of the use cases and makes handlers easy to test with a fake context. Enums are stored as strings;
all measurements are stored in **meters**.

## Aspire's role

`LoopQuest.AppHost` is the project you run in development. It:

- starts a **PostgreSQL container** and creates the `loopquestdb` database,
- launches the **api** project and injects the DB connection string into it,
- waits for Postgres to be healthy before starting the API,
- opens the **Aspire dashboard** (logs, traces, metrics, resource health).

`ServiceDefaults` (referenced by the API) adds OpenTelemetry, `/health` + `/alive` endpoints, HTTP
resilience, and service discovery with one `AddServiceDefaults()` call.

In production the same model maps to Azure (App Service + managed PostgreSQL); TLS terminates at the
edge, which is why the API itself speaks plain HTTP in-cluster (no `UseHttpsRedirection`).

## Solution-wide configuration

- **`Directory.Build.props`** — common MSBuild settings (nullable, implicit usings, analysis level)
  applied to every project, so they aren't repeated per `.csproj`.
- **`.editorconfig`** — code style, enforced at build time (`EnforceCodeStyleInBuild`), with a few
  deliberate analyzer relaxations documented inline.
- **`.config/dotnet-tools.json`** — local tool manifest (currently `dotnet-ef`); run
  `dotnet tool restore` after cloning.

## Testing strategy

- **Unit tests** (`LoopQuest.Domain.Tests`) — fast, no I/O. The home of the game-rule tests (e.g. the
  `ChallengeCalculator`). This is where most tests should live.
- **Integration tests** (`LoopQuest.Api.IntegrationTests`) — boot the real app graph through the
  Aspire AppHost (real Postgres container) and exercise endpoints over HTTP. Slower; needs Docker.
  Reserve for "the wiring actually works" smoke coverage of key flows.
