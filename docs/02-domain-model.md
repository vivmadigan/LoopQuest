# 02 — Domain Model & Game Rules (authoritative)

This is the source of truth for the deterministic rules. When code and prose disagree, this document
and its tests win. All distances and elevations are in **meters** unless stated otherwise.

---

## Entities

### User (single user in v1, modelled for multi-user later)
- `Id` (Guid)
- `StravaAthleteId` (long, unique)
- `DisplayName` (string)
- `TimeZoneId` (string, IANA; default `Europe/Stockholm`)
- `CreatedAt` (DateTimeOffset)
- owned `StravaConnection`: `AccessToken`, `RefreshToken`, `ExpiresAt`, `Scope`

### Loop (the library) — **implemented in Stage 0**
- `Id`, `Name`, `Description`
- `Category` (enum: `Classic`, `Urban`, `Fantasy`)
- `Tier` (enum: `Easy`, `Moderate`, `Hard`, `Epic`)
- `TargetDistanceMeters` (double), `TargetElevationMeters` (double)
- `RealWorldReference` (string?), `ImageUrl` (string?), `IsActive` (bool)

### Activity (private, ingested from Strava)
- `Id`, `UserId`
- `StravaActivityId` (long, unique)
- `Name`, `SportType` (string)
- `StartDateLocal` (DateTimeOffset — used for week bucketing)
- `DistanceMeters`, `ElevationGainMeters` (from Strava `total_elevation_gain`)
- `IngestedAt`

### WeeklyChallenge
- `Id`, `UserId`, `LoopId`
- `WeekStart` (DateOnly, Monday), `WeekEnd` (DateOnly, Sunday)
- `Status` (enum: `Active`, `Completed`, `Failed`)
- `SelectedAt`, `CompletedAt?`
- `SnapshotTargetDistanceMeters`, `SnapshotTargetElevationMeters`
- `AchievedDistanceMeters`, `AchievedElevationMeters`, `ElevationPurchasedMeters`

> **Snapshot the loop's targets at selection time.** Editing the loop library later must never
> retroactively change a past or active challenge.

---

## The week

- A week runs **Monday 00:00 → Sunday 23:59:59**.
- An activity belongs to a week by its **start time**.
- Bucket by the activity's **local** start time (Strava's `start_date_local`), not UTC. A run in
  Zermatt buckets by Swiss time, a run in Australia by Australian time.
- A challenge can be picked any day. Activities already logged earlier that same week still count.

## What counts

Qualifying `sport_type` values are summed for both distance and elevation:

- `Run`, `TrailRun`, `Hike`, `Walk`

Hikes and walks count because steep hiking is a legitimate way to accumulate vertical.
`VirtualRun` (treadmill) is **excluded** by default — a one-line config change if you want it in.

## The loop's two targets

Every loop is two numbers: `TargetDistanceMeters` and `TargetElevationMeters`. This is true whether
the loop is a real race, a route measured off a map, or pure invention.

## Progress & completion

Across the week the app maintains:

- `achieved_distance_m`  = Σ qualifying activity distances
- `achieved_elevation_m` = Σ qualifying activity elevation gains

The two targets are tracked **independently and both must be satisfied** — with one nuance below.

---

## Elevation substitution (the signature rule)

If you've run **more distance than required** but are short on climbing, surplus distance can buy a
**limited** amount of the missing elevation. Substitution is **one-directional**: extra distance can
buy elevation, but extra elevation can never buy distance.

Two parameters:

- **Exchange rate** — 3 km of surplus distance buys 100 m of elevation (i.e. **30 m of distance per
  1 m of elevation**). Deliberately ~3× the real-world effort cost, so real climbing always stays the
  efficient path and flat running is a grind, not a shortcut.
- **Cap** — you can buy at most **one third** of the loop's required elevation this way. So every loop
  always demands two thirds of its elevation as genuine climbing.

### Algorithm (meters)

```text
Constants:
  SUBSTITUTION_RATE_M_PER_M = 30          // 30 m distance buys 1 m elevation (3 km per 100 m)
  MAX_BUYABLE_FRACTION      = 1.0 / 3.0   // buy at most 1/3 of required elevation

surplus_distance_m      = max(0, achieved_distance_m - target_distance_m)
elevation_affordable_m  = surplus_distance_m / SUBSTITUTION_RATE_M_PER_M
max_buyable_elevation_m = target_elevation_m * MAX_BUYABLE_FRACTION
elevation_purchased_m   = min(elevation_affordable_m, max_buyable_elevation_m)
effective_elevation_m   = achieved_elevation_m + elevation_purchased_m

distance_complete   = achieved_distance_m   >= target_distance_m
elevation_complete  = effective_elevation_m >= target_elevation_m
challenge_complete  = distance_complete AND elevation_complete
```

Surplus distance is *spent* on elevation but never deducted from the distance bar — only the overflow
above the target is used, so the distance requirement stays satisfied.

### Worked examples

- **Alpine loop, 50 km / 3000 m** → max buyable = 1000 m, which needs 30 km of surplus (an 80 km
  week). The other 2000 m must be real climbing. UTMB-class loops become genuine boss levels.
- **Urban loop, 49 km / 300 m** → max buyable = 100 m. A near-flat big-mileage week essentially
  finishes it on distance (you still need 200 m of real climbing for the two-thirds floor).

This split is intentional: small/flat loops are achievable anywhere; marquee alpine loops are
hard-locked behind actual vertical.

> This is your **Stage 1 exercise**. The canonical behaviour is encoded in the (currently skipped)
> tests in `tests/LoopQuest.Domain.Tests/ChallengeCalculatorTests.cs`, against the contract in
> `src/LoopQuest.Domain/Challenges/ChallengeCalculator.cs`.

---

## Weekly challenge selection (calibration)

At the start of a week, read last week's totals and offer three options of escalating difficulty.

```text
D_last = last week's total qualifying distance (m)
E_last = last week's total qualifying elevation (m)
E_last_floored = max(E_last, 50)         // guard against a flat week

Target shapes (multipliers tunable):
  Safe  -> D_last * 0.90 , E_last_floored * 0.90
  Step  -> D_last * 1.10 , E_last_floored * 1.10
  Reach -> D_last * 1.30 , E_last_floored * 1.30

For each target (Dt, Et), score every active loop by relative closeness:
  score = |loop.distance - Dt| / Dt + |loop.elevation - Et| / Et    // lower is better
Pick the lowest-scoring loop per tier; de-duplicate so the three differ.

Cold start (no previous week):
  Offer fixed seed targets (e.g. 20 km/300 m, 30 km/600 m, 40 km/1000 m),
  OR let the user browse the library and pick freely.
```

Keep the **reach** tier modest — aggressive week-on-week jumps cause volume-spike injuries, so
conservative calibration is a feature, not a limitation.

## Completion & failure (v1)

At week end a challenge resolves to exactly one of:

- **Completed** — both targets met
- **Failed** — week ended, targets not met

No streaks, partial credit, or carry-over in v1.

---

## Tunable defaults (confirm or override)

| # | Decision | Default |
|---|---|---|
| 1 | Calibration multipliers | 0.90 / 1.10 / 1.30 |
| 2 | Cold start | fixed seed targets |
| 3 | Failure semantics | binary Completed/Failed, no streaks |
| 4 | Sport-type allowlist | Run, TrailRun, Hike, Walk (VirtualRun excluded) |
| 5 | Timezone for week boundaries | bucket by each activity's local start time |
