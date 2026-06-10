# 03 — Roadmap (the staged build plan)

This is the plan we work through together, one stage at a time. Each stage is small enough to finish
in a sitting, ends in something you can run, and builds on the last. **You write the code**; Claude
helps with design, hints, review, and the occasional reference example.

For each stage: a **goal**, the **work** split by layer, the **tests**, and a **Definition of Done**.

> Legend: ✅ done · ▶️ next · ⬜ later · 🧩 your hands-on exercise

Dependencies are noted so you can reorder if you like, but the default order is the gentlest learning curve.

---

## ✅ Stage 0 — Walking skeleton (complete)

Solution, four clean-architecture layers, Aspire orchestration, PostgreSQL via EF Core, the
`GET /api/loops` reference slice end-to-end, seed data, xUnit unit + integration tests, OpenAPI/Scalar.

**Verify:** `dotnet run --project src/LoopQuest.AppHost`, open the dashboard, hit `/api/loops`.

---

## ✅ 🧩 Stage 1 — The domain core: elevation substitution (complete)

**Goal:** implement the game's signature rule as pure, well-tested domain logic.

This is deliberately first: it's the most important and most interesting logic, it needs no database
or HTTP, and it's perfect for test-driven development.

**Work — Domain only:**
- Implement `ChallengeCalculator.Calculate` in `src/LoopQuest.Domain/Challenges/ChallengeCalculator.cs`
  per the algorithm in [02-domain-model.md](02-domain-model.md#elevation-substitution-the-signature-rule).

**Tests:** make the six skipped tests in `tests/LoopQuest.Domain.Tests/ChallengeCalculatorTests.cs`
pass — one at a time (remove a `Skip`, watch it go red, implement just enough to make it green).

**Hints:** the result you return (`ChallengeProgress`) carries every field the tests assert on. Watch
the `max(0, …)` on surplus and the `min(…)` for the cap. Don't deduct surplus from distance.

**Definition of Done:** all of `dotnet test tests/LoopQuest.Domain.Tests` is green (12 passed, 0 skipped);
no other layer changed.

---

## ▶️ Stage 2 — Connect to Strava (OAuth)

**Goal:** connect exactly one athlete and store refreshable tokens. *(Depends on: nothing in code; needs
a Strava API application — create one at https://www.strava.com/settings/api.)*

**Plan:** [plans/stage-2-strava-oauth.md](plans/stage-2-strava-oauth.md)

**Work:**
- **Domain:** `User` entity + owned `StravaConnection` value object (see [02](02-domain-model.md)).
- **Application:** `IStravaClient` abstraction; `GetStravaAuthUrlQuery`; `CompleteStravaConnectionCommand`
  (exchange auth code → tokens → persist).
- **Infrastructure:** `StravaClient` (typed `HttpClient`) implementing the token exchange; EF config +
  migration for `User`/`StravaConnection`; read client id/secret from configuration (user-secrets locally).
- **Api:** `GET /auth/strava/connect` (redirect to Strava authorize) and `GET /auth/strava/callback`
  (handle `code`, store tokens, redirect to the dashboard).

**Notes:** scope `activity:read_all`. Add token **refresh** (refresh when `ExpiresAt` is near) — you can
build the bare refresh here and lean on it in Stage 3.

**Tests:** unit-test the auth-URL builder and the token-response mapping (fake `HttpMessageHandler`).

**DoD:** you can click connect, authorize on Strava, land back on the app, and see a stored connection.

---

## ⬜ Stage 3 — Sync activities

**Goal:** pull recent activities from Strava and store them, de-duplicated. *(Depends on: Stage 2.)*

**Plan:** [plans/stage-3-sync-activities.md](plans/stage-3-sync-activities.md)

**Work:**
- **Domain:** `Activity` entity.
- **Application:** `SyncActivitiesCommand` — call `IStravaClient` for activities `after` a given epoch,
  map `sport_type`/`distance`/`total_elevation_gain`/`start_date_local`, **upsert by `StravaActivityId`**.
  Filter to the qualifying sport types.
- **Infrastructure:** implement the paged `GET /api/v3/athlete/activities` call; EF config + migration
  for `Activity`; ensure token refresh runs before the call.
- **Api:** `POST /api/sync`.

**Tests:** unit-test the sport-type filter and the upsert (no duplicates on re-sync). Add an integration
test if you mock Strava.

**DoD:** `POST /api/sync` pulls your real activities once and is idempotent on a second call.

---

## ⬜ Stage 4 — Select a weekly challenge

**Goal:** turn a chosen loop into an active `WeeklyChallenge` for the current week. *(Depends on: loop
library ✅; week math.)*

**Work:**
- **Domain:** `WeeklyChallenge` entity; a small `Week` helper (Mon–Sun boundaries from a date + timezone).
  **Snapshot** the loop's targets onto the challenge at selection.
- **Application:** `SelectChallengeCommand { loopId }` + validator; create the active challenge if none
  exists for this week.
- **Infrastructure:** EF config + migration.
- **Api:** `POST /api/challenge/select`.

**Tests:** week-boundary unit tests (incl. an activity on Sunday 23:59 vs Monday 00:00 in different
timezones); selection rejects a second active challenge for the same week.

**DoD:** you can select a loop and get back an active challenge with snapshotted targets.

---

## ⬜ Stage 5 — Live progress for the current challenge

**Goal:** show real progress by combining the week's activities with the substitution rule. *(Depends
on: Stages 1, 3, 4.)*

**Work:**
- **Application:** `GetCurrentChallengeQuery` — sum qualifying activities in the challenge's week, feed
  totals + snapshot targets into `ChallengeCalculator.Calculate`, return progress (distance, effective
  elevation, purchased elevation, completion flags).
- **Api:** `GET /api/challenge/current`.

**Tests:** handler test with seeded activities asserting the computed progress matches the calculator.

**DoD:** `GET /api/challenge/current` returns correct, live progress as you sync more activities.

---

## ⬜ Stage 6 — Calibrated weekly options

**Goal:** offer three escalating options based on last week. *(Depends on: Stages 1, 3; library ✅.)*

**Work:**
- **Application:** `GetChallengeOptionsQuery` — compute last week's totals, build Safe/Step/Reach
  targets, score loops by relative closeness, pick + de-duplicate (algorithm in [02](02-domain-model.md#weekly-challenge-selection-calibration)).
  Handle the cold-start case.
- **Api:** `GET /api/challenge/options`.

**Tests:** scoring picks the nearest loop; de-duplication; cold-start path; flat-week elevation floor.

**DoD:** `GET /api/challenge/options` returns three sensible, distinct options.

---

## ⬜ Stage 7 — Weekly rollover & history

**Goal:** finalize last week automatically and keep a conquest history. *(Depends on: Stages 4, 5.)*

**Work:**
- **Infrastructure:** a hosted `BackgroundService` using `PeriodicTimer` that, at the Monday boundary
  in the user's timezone, sets the finished challenge to `Completed`/`Failed` and archives it.
- **Application:** `GetHistoryQuery`.
- **Api:** the history is surfaced publicly in Stage 8; add a private history read here if useful.

**Tests:** finalize logic (a challenge that met both targets → Completed; otherwise Failed) as a unit test
over a pure method, so you don't need to test the timer itself.

**DoD:** a finished week flips to Completed/Failed without manual action; history is queryable.

---

## ⬜ Stage 8 — Public read-only profile

**Goal:** share game state publicly **without** exposing raw activity data. *(Depends on: Stages 5, 7.
Read [04-strava-integration.md](04-strava-integration.md) first.)*

**Work:**
- **Application:** `GetPublicProfileQuery` (active loop + progress + summary) and the public history.
- **Api:** `GET /api/public/profile`, `GET /api/public/history` (no auth). **No public `Activity` endpoint.**

**Tests:** assert the public DTOs contain only derived game state — never per-activity rows.

**DoD:** the public endpoints show the game; the raw runs stay private.

---

## ⬜ Stage 9 — Periodic background sync

**Goal:** keep progress fresh without a manual sync. *(Depends on: Stage 3.)*

**Work:** extend/add a `BackgroundService` that runs `SyncActivitiesCommand` every few hours.
Webhooks remain deferred — polling is enough for v1.

**DoD:** activities appear without pressing Sync.

---

## ⬜ Stage 10 — React + TypeScript frontend (later)

Dashboard (two progress bars — distance fills horizontally, elevation vertically, with the
purchased-via-distance portion in a distinct shade), challenge picker, public profile, history.
We'll likely fold the SPA into the Aspire AppHost as another resource. Use the Context7 MCP for
current React/Vite/Router guidance when we get here.

---

## Working agreement for each stage

1. Skim the spec rule and the relevant doc section.
2. Write the test(s) first where it's natural (always for Domain).
3. Implement the slice following the `GetLoops` shape.
4. `dotnet build` + `dotnet test`; run the app and hit the endpoint.
5. Commit with a clear message. Then move on.
