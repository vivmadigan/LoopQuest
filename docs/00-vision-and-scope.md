# 00 — Vision & Scope

## The idea

Weekly training volume is an abstract number that's easy to ignore. LoopQuest reframes it as a
**destination**. Each week you're offered a few named **loops** — famous trail races, urban
multi-laps, fantasy summits — you pick one, and every run, hike, and walk you log that week adds
toward completing it.

A loop is defined by **two targets**: a distance and an amount of climbing. **Both** must be met.

> The hook: *"Did I conquer the Mont Blanc loop this week?"* reads better than *"I ran 47 km and
> climbed 1,800 m."*

## The cycle

**Last week chooses the challenge; this week completes it.** At the start of a week the app reads
your previous week's totals, gauges what you're capable of, and surfaces three calibrated options of
escalating difficulty. You pick one and chase it until Sunday.

This calibration is also a kindness: aggressive week-on-week jumps are what cause volume-spike
injuries, so the "reach" option stays modest on purpose.

## Who it's for (v1)

A single user — you. You connect one Strava account. A public, read-only profile page lets other
people watch your progress as **game state** (which loop, how far along, what you've conquered)
without ever exposing your raw activity feed. See
[04-strava-integration.md](04-strava-integration.md) for why that boundary matters.

## In scope for v1

- Strava connect (single-player OAuth) and weekly activity ingestion
- A seeded **loop library**
- **Calibrated** weekly options and selection
- Two-dimensional progress with the **capped, lossy elevation substitution**
- Weekly rollover + conquest history
- A public read-only profile
- A basic dashboard with the signature two-bar visualization (frontend, later)

## Out of scope for v1 (deliberately deferred)

- Multiplayer and cross-player views
- Strava webhooks (polling is enough for v1)
- Streaks, partial credit, richer reward semantics
- Leaderboards / social comparison
- LLM "epic saga" narration of a week
- Loop suggestion via embeddings (the matching is two numbers and arithmetic — not needed)

The data model and the single-player + public-profile shape are designed to extend to these cleanly.

## What "done" looks like for v1

You can connect Strava, get three sensible weekly options based on last week, pick one, watch both
progress bars fill as you sync activities, have the week finalize automatically on Monday, and share
a public page showing your conquests — all without ever leaking raw Strava activity data publicly.
