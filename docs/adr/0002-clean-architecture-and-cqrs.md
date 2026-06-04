# ADR-0002 — Clean Architecture with CQRS (MediatR)

- **Status:** Accepted
- **Date:** 2026-06-04

## Context

LoopQuest has a small but genuinely interesting domain core (the elevation-substitution rule, week
bucketing, calibration) surrounded by infrastructure concerns (Strava, PostgreSQL, background jobs).
The owner is learning patterns common in professional .NET shops and relevant to interviews.

## Decision

Use **Clean Architecture** with four layers — Domain, Application, Infrastructure, Api — and the
dependency rule pointing inward. Within the Application layer, use **CQRS via MediatR**: one
request + handler per use case, with cross-cutting concerns implemented as **pipeline behaviours**
(validation via FluentValidation, logging).

The Application depends on abstractions (`IAppDbContext`, later `IStravaClient`) that Infrastructure
implements, so the core never references EF Core concretely or knows about PostgreSQL or HTTP.

## Alternatives considered

- **Plain layered services** (no mediator) — simpler, fewer moving parts, but loses the uniform
  pipeline for cross-cutting concerns and is less aligned with what the owner wants to learn.
- **Vertical slice architecture with a free mediator** — attractive, but Clean Architecture + MediatR
  is the more widely recognised pattern for this learning goal.

## Consequences

- Each use case is small, discoverable, and unit-testable in isolation.
- Validation/logging/(later) transactions live in one place, not copy-pasted into handlers.
- More ceremony than a minimal service approach — acceptable for the learning objective.
- Couples the Application to MediatR; mitigated by pinning a free version (see ADR-0004).
