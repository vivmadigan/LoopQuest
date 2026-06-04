# ADR-0005 — Controllers over Minimal APIs

- **Status:** Accepted
- **Date:** 2026-06-04

## Context

The product spec sketched Minimal APIs, but the owner explicitly wants hands-on experience with
**MVC-style controllers**, which remain very common in enterprise .NET codebases and interviews.

## Decision

Build the HTTP surface with **attribute-routed controllers** (`[ApiController]`, `ControllerBase`),
scaffolded via `dotnet new webapi --use-controllers`. Controllers stay **thin**: they translate HTTP
to a MediatR request and back, with no business logic.

## Consequences

- Familiar, widely-used controller patterns (model binding, `ActionResult<T>`, `[ProducesResponseType]`).
- Slightly more ceremony than Minimal APIs per endpoint.
- Because controllers only dispatch to MediatR, switching to Minimal APIs later (if ever desired) would
  be mechanical — the Application layer wouldn't change at all.
