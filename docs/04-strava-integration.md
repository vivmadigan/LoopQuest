# 04 — Strava Integration

> Not built yet — this is the design to follow in Stages 2–3 and 9. Read the data boundary section
> before building the public profile (Stage 8).

## Mode: single player

The app connects exactly **one** athlete (you). Public viewers never connect Strava — they only view a
page — so Strava's per-app athlete cap is never an issue.

## OAuth

- **Scope:** `activity:read_all` (includes private activities).
- **Connect flow:**
  1. `GET /auth/strava/connect` → redirect to the Strava authorize URL.
  2. Strava redirects back to `GET /auth/strava/callback?code=…`.
  3. Exchange the `code` for tokens at the token endpoint.
  4. Store them in the owned `StravaConnection` (`AccessToken`, `RefreshToken`, `ExpiresAt`, `Scope`).
- **Token refresh:** before any Strava call, if `ExpiresAt` is past or near, refresh via
  `grant_type=refresh_token` and persist the new tokens.

## Activity pull

- `GET /api/v3/athlete/activities` with an `after` epoch (start of the current week, or the last
  successful sync) and pagination.
- Map `sport_type`, `distance`, `total_elevation_gain`, and `start_date_local`.
- **Upsert by `StravaActivityId`** so re-syncing never creates duplicates.
- Keep only the qualifying sport types (`Run`, `TrailRun`, `Hike`, `Walk`).

## Sync triggers

- On-demand: a Sync button / on dashboard load (`POST /api/sync`).
- Scheduled: a periodic background sync every few hours (Stage 9).
- **Webhooks are deferred** — polling is sufficient for v1.

## Configuration & secrets

Strava **client id** and **client secret** are secrets. Locally, use .NET user-secrets (never commit them):

```bash
dotnet user-secrets init   --project src/LoopQuest.Api
dotnet user-secrets set "Strava:ClientId" "<id>"         --project src/LoopQuest.Api
dotnet user-secrets set "Strava:ClientSecret" "<secret>" --project src/LoopQuest.Api
```

In Azure these become app settings / Key Vault references. The redirect/callback URL you register in
the Strava app settings must match your running callback (e.g. `http://localhost:<port>/auth/strava/callback`).

## The public/private data boundary (important)

Strava's API agreement restricts a third-party app to showing a user's Strava activity data **only to
that user**. The line we hold:

- **Public is fine** — your *derived game state*: which loop you're on, progress totals and
  percentages, completion status, and the history of loops conquered. This is your own transformed data.
- **Keep private** — the *raw underlying activity feed*: the individual list of runs with their
  Strava-sourced distances and routes must not be visible to anyone else.

> Show the game to the world; keep the raw runs to yourself. The progress bar is public; the activity
> list behind it is not. This is exactly why `Activity` is private data with **no public endpoint**,
> and why the public profile (Stage 8) only ever exposes aggregated game state.

This boundary is also why the deferred "LLM epic-saga narration" idea is a grey zone (it would feed
Strava-derived data to a model) and would be safer built on self-exported FIT data than on API data.
