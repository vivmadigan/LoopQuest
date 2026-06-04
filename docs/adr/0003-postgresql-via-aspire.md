# ADR-0003 — PostgreSQL via .NET Aspire

- **Status:** Accepted
- **Date:** 2026-06-04

## Context

The app needs a relational store (loops, users, activities, weekly challenges). The hosting target is
Azure, where managed PostgreSQL is a first-class, cost-effective option. The owner also wants to learn
**.NET Aspire** for local orchestration and cloud-readiness.

## Decision

Use **PostgreSQL** with **EF Core** (Npgsql provider), wired through **.NET Aspire**:

- The **AppHost** declares a Postgres container resource and the `loopquestdb` database, and references
  it from the API (`WithReference` + `WaitFor`).
- The **API/Infrastructure** consumes it via `AddNpgsqlDbContext<AppDbContext>("loopquestdb")`
  (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`), which adds pooling, retries, health checks,
  logging, and telemetry.

Locally this needs Docker (Aspire runs Postgres in a container). In Azure the same model points at a
managed PostgreSQL instance with no application code changes — only the connection source differs.

## Alternatives considered

- **SQLite** — zero-infrastructure and great for quick local runs, but diverges from the Azure/Postgres
  target and would mean swapping providers later. EF Core's provider model keeps SQLite available as a
  fallback if we ever want it.
- **SQL Server** — fine on Azure but heavier locally and not the chosen target.

## Consequences

- Local dev requires Docker running.
- Production parity is high (same engine locally and in the cloud).
- Aspire gives observability and health wiring "for free," which also makes integration testing
  straightforward (the test boots the real graph).
- We accept a dependency on Aspire's versioning cadence (currently 13.x).
