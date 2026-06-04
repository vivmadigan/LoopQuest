# ADR-0004 — MediatR version & licensing (pin to 12.5.0)

- **Status:** Accepted
- **Date:** 2026-06-04

## Context

We use MediatR for CQRS dispatch (ADR-0002). MediatR **v13+** moved to a **commercial licence**
(free for individuals and small organisations, but with licensing terms and notices). MediatR
**12.x** is the last freely/openly licensed line, and its API (`AddMediatR`,
`RegisterServicesFromAssembly`, `IPipelineBehavior`, `ISender`) is identical to what we use.

Separately, **FluentAssertions v8** also became commercial; we avoid it entirely and use plain xUnit
`Assert` (Shouldly is a free alternative if we later want fluent syntax).

## Decision

Pin **`MediatR` to `12.5.0`** explicitly in `LoopQuest.Application.csproj`. Do not let it float to
v13+.

## Consequences

- No licensing ambiguity for a learning/personal project.
- The code is identical to what a v13 codebase would look like, so the learning transfers.
- If the project ever needs v13 features or commercial support, revisit this ADR and accept the licence.
- Keep an eye out for transitive pulls of MediatR v13 from other packages (none today).
