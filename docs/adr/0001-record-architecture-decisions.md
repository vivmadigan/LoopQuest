# ADR-0001 — Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-06-04

## Context

This is a learning-focused, professionally-structured project. Decisions made early (architecture,
data store, libraries) shape everything downstream, and it's valuable — both for learning and for
future-you — to capture *why* a choice was made, not just *what* was chosen.

## Decision

We keep lightweight **Architecture Decision Records** in `docs/adr/`, one file per significant
decision, using a short format: Status, Context, Decision, Consequences. Numbered sequentially.

A decision is "significant" if reversing it later would be costly or contentious (frameworks, data
store, cross-cutting patterns, notable library choices).

## Consequences

- New contributors (and AI agents) can understand the reasoning quickly.
- Superseded decisions are kept for history and marked `Superseded by ADR-XXXX`, not deleted.
- Trivial or easily-reversible choices don't need an ADR — keep the noise low.
