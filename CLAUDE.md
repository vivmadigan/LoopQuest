# CLAUDE.md — working agreement for this repository

Guidance for Claude (and any AI agent) collaborating on LoopQuest. Read this first.

## What this project is

LoopQuest is the **.NET 10 backend** for a weekly running-challenge game built on Strava data.
A React + TypeScript frontend comes later. The full product spec defines the game; the
**authoritative game rules** are restated in [docs/02-domain-model.md](docs/02-domain-model.md),
and the build plan lives in [docs/03-roadmap.md](docs/03-roadmap.md).

## How to collaborate here (important)

The owner is a **junior developer using this project to learn**. The goal is not just a finished
app — it's their understanding. Therefore:

- **Do not silently complete whole features.** Prefer to explain the approach, implement a clear
  reference example, and leave well-scoped pieces for them to write. When you do write code,
  explain *why*, not just *what*.
- When they're stuck on an exercise (e.g. the `ChallengeCalculator`), **guide with hints and point
  at the tests** before handing over a full solution. Offer to show it only if asked.
- Keep changes small and reviewable. One concept at a time.
- Match the existing style and patterns in the codebase (see Conventions below).
- Be honest about uncertainty and about what you did and didn't verify (build/tests).

## Architecture rules (non-negotiable)

Clean Architecture with the dependency arrow pointing **inward**:

```
Api → Infrastructure → Application → Domain
```

- **Domain** (`src/LoopQuest.Domain`): entities, enums, value objects, pure game rules. **No
  dependencies on other projects or frameworks.** Business invariants live here.
- **Application** (`src/LoopQuest.Application`): CQRS use cases (MediatR `IRequest` + handler), DTOs,
  abstractions (e.g. `IAppDbContext`, later `IStravaClient`), and pipeline behaviours. Depends on
  Domain (and EF Core *abstractions* only).
- **Infrastructure** (`src/LoopQuest.Infrastructure`): EF Core `DbContext` + configurations + migrations
  + seed, the Strava HTTP client (later), background services. Implements Application abstractions.
- **Api** (`src/LoopQuest.Api`): thin controllers, `Program.cs` composition root, OpenAPI/Scalar,
  exception handling. Translates HTTP ↔ MediatR.
- **AppHost** / **ServiceDefaults**: Aspire orchestration and shared service wiring.

If a change wants to make an inner layer depend on an outer one, that's a design smell — stop and rethink.

## Conventions

- **Controllers, not Minimal APIs.** (Deliberate — the owner wants controller experience.)
- **One vertical slice per use case**, mirroring `Loops/Queries/GetLoops/`: `Query`/`Command` +
  `Handler` + `Dto` (+ `Validator` when there are inputs). Copy that folder's shape.
- **Units:** store distance and elevation in **meters** everywhere; convert to km only at the
  display edge (frontend). Times: bucket weeks by the activity's **local** start time.
- **Entities** use private setters + a static `Create(...)` factory that enforces invariants; EF
  binds via a private parameterless constructor.
- **Enums** are persisted as **strings** (`HasConversion<string>()`).
- **Never return EF entities from the API** — always project to a DTO.
- File-scoped namespaces; `_camelCase` private fields; primary constructors where they read well.
- Validation throws `FluentValidation.ValidationException` in a pipeline behaviour; the API's
  `ValidationExceptionHandler` maps it to an RFC 9457 problem-details 400.

## Build / test / run / migrate

```bash
dotnet tool restore                                   # restore dotnet-ef (local tool)
dotnet build
dotnet run --project src/LoopQuest.AppHost            # run everything via Aspire (needs Docker)
dotnet test                                           # all tests (integration tests need Docker)
dotnet test tests/LoopQuest.Domain.Tests              # fast unit tests only

# New migration (Infrastructure is its own startup, via the design-time factory):
dotnet ef migrations add <Name> \
  --project src/LoopQuest.Infrastructure \
  --startup-project src/LoopQuest.Infrastructure \
  --output-dir Persistence/Migrations
```

The API migrates + seeds on startup (`DatabaseInitializer`). That's a deliberate MVP shortcut;
moving migration to a dedicated step is a roadmap item.

## Pinned decisions (don't change without discussion — see docs/adr)

- **.NET 10**, **Aspire 13.x**, **EF Core 10**, **PostgreSQL** (Npgsql).
- **MediatR pinned to `12.5.0`** — v13+ is commercially licensed; 12.x is free with the same API.
- **No FluentAssertions** (v8 is commercial). Use xUnit `Assert`; Shouldly is an OK free upgrade.
- **Central Package Management (CPM) is enabled** — every package version lives in the root
  `Directory.Packages.props`; each `.csproj` references packages by name only (no inline `Version`).

## MCP tools

A dedicated MCP usage guide governs tool use (Microsoft Learn for .NET/Aspire/EF Core guidance;
Glider for C# semantic navigation and safe refactors; NuGet for package inspection; Context7 for
React/JS docs later; Chrome DevTools + Playwright for frontend; GitHub for repo/PR work). Use them
**deliberately**, not reflexively. For anything version-sensitive in .NET 10 / Aspire, **verify
against Microsoft Learn** rather than relying on memory. Never update dependencies or modify
external systems (GitHub, etc.) without explicit approval.

## Git workflow

Three-tier branching:

- **`main`** — stable, always-green. Only receives merges from `dev` at milestones (e.g. a completed
  stage or release). Don't commit directly to `main`.
- **`dev`** — the integration branch and default home base. Feature branches merge here.
- **`feature/*`** — one short-lived branch per piece of work, branched off `dev`. Naming:
  `feature/stage-<n>-<slug>`, e.g. `feature/stage-1-challenge-calculator`. Merge back to `dev` when
  the change builds, tests pass, and it's reviewed.

```bash
# Start a feature (from dev)
git switch dev && git switch -c feature/stage-1-challenge-calculator
# ... work, commit ...
git switch dev && git merge --no-ff feature/stage-1-challenge-calculator
# At a milestone, promote dev to main
git switch main && git merge --no-ff dev
```

Trivial docs/config tweaks may go straight to `dev`. There's no GitHub remote yet — when we add one,
feature branches become pull requests into `dev`.

## Definition of done for a change

1. It builds (`dotnet build`) with no new errors.
2. Tests pass (`dotnet test`), and new behaviour has tests.
3. The dependency rule is respected.
4. You've told the owner what you changed, why, and what you verified.
