# Stage 6 Plan — Calibrated Weekly Options

> **How to use this doc:** work top to bottom. Every task is tagged:
> **🧩 you write it** · **📖 reference example — type it, adapt it, understand it** · **🤝 we do it together in chat**.
> Drafted by Claude (2026-06-23), in the richer style of the Stage 5 plan: every step opens with
> *"the plan, in plain words"* (what it is **and where the code gets used**), and every code block is
> followed by a *"what each line does / what's easy to get wrong"* walkthrough.
>
> **Vocabulary reminder:** a *Stage* is a roadmap chapter (this doc is Stage 6); a *Step* is a numbered
> task inside it.
>
> **When a step feels foggy**, the rescue order is: ① that step's *"the plan, in plain words"* opener
> ② its walkthrough ③ the domain model's [Weekly challenge selection (calibration)](../02-domain-model.md#weekly-challenge-selection-calibration)
> (the authoritative algorithm) ④ ask Claude *"what do I actually type?"*.
>
> **Dependencies:** needs synced activities (Stage 3), the `Week` helper + injected clock (Stage 4), and
> reuses the in-memory week-summing pattern you wrote in Stage 5. All merged, so:
> `git switch dev && git switch -c feature/stage-6-challenge-options`

---

## 1. What you're building

`GET /api/challenge/options` answers: **"Given how I ran last week, what three challenges should I be
offered this week?"** It reads last week's totals, builds three escalating targets (Safe / Step / Reach),
and picks the loop from the library that best fits each — three distinct, calibrated suggestions.

```
GET /api/challenge/options
   │
   ▼
GetChallengeOptionsQuery        ← no input, so NO validator
   │
   ▼
GetChallengeOptionsQueryHandler (Application)   ctor: (IAppDbContext db, TimeProvider clock)
   │ 1. load the single user                       (TimeZoneId)
   │ 2. lastWeek = Week.Current(tz, now).Previous()   ← the week BEFORE this one
   │ 3. sum last week's qualifying activities → D_last, E_last   (same pattern as Stage 5)
   │ 4. load the active loops                       (the pool to choose from)
   │ 5. ChallengeCalibrator.BuildOptions(D_last, E_last, loops)   ← the pure heart (Domain)
   │ 6. project the 3 chosen options → DTOs
   ▼
IReadOnlyList<ChallengeOptionDto>   (Safe / Step / Reach, each a DISTINCT loop)
```

**Why this stage matters:** every stage so far reacted to a loop *you* named. Stage 6 is the first time
the app makes a *suggestion* — it looks at your training and proposes a sensible next goal. It's the
"calibration is a kindness" idea from the vision: the Reach option stays modest on purpose, because
aggressive week-on-week jumps cause injuries. Note what this stage does **not** do: it doesn't select
anything (that's still Stage 4's `POST /select`) and it writes nothing to the database. Like Stage 5,
it's a **read** — but this time the interesting work is a pure calculation, so the heart of it is a
dependency-free Domain class you can unit-test exactly like the Stage 1 `ChallengeCalculator`.

### The complexities of this stage, in plain words

1. **Two unrelated meanings of "tier."** This is the #1 confusion of the stage, so meet it head-on:
   - **Calibration tier** = the three *options* — **Safe / Step / Reach**. New to this stage.
   - **Loop tier** = a loop's *difficulty band* — **Easy / Moderate / Hard / Epic**. Already on the `Loop`
     entity, **and the calibration maths does not use it** (the `LoopTier` doc-comment says so outright).

   The calibration scores loops purely on their distance and elevation numbers. Keep the two "tiers"
   mentally separate and the stage gets much simpler.
2. **"Last week," not "this week."** The whole point is to react to the week that just finished. You get
   it with a tiny new helper, `Week.Previous()`. Summing *this* week would calibrate off a week still in
   progress — a silent bug.
3. **Relative closeness, not absolute.** A loop "fits" a target by *percentage* distance on each axis,
   not raw metres: `|loop − target| / target`. Without dividing by the target, a giant alpine loop's raw
   numbers would swamp the score and always look "far," and the nearest small loop would never win.
4. **Greedy de-duplication.** Three targets could all want the same loop. We fill Safe → Step → Reach in
   order, each taking its nearest loop *not already taken*, so the three options are always distinct
   (your chosen behaviour). If the library is too small, we return fewer rather than repeat.
5. **Cold start.** A brand-new user (or a complete rest week) has no last-week numbers to scale from, so
   we fall back to **fixed seed targets** (20 km/300 m, 30 km/600 m, 40 km/1000 m) and score against
   those. This also dodges a divide-by-zero (a `0` target).

### New words in this stage

| Word | What it actually is | You'll meet it in |
|---|---|---|
| Calibration tier | the three escalating options: `Safe` / `Step` / `Reach`. A new enum, unrelated to `LoopTier` | Step 2 calibrator |
| Calibrated target | the (distance, elevation) the algorithm *aims* for in a tier, before matching a real loop to it | Step 2 |
| Relative score | percentage closeness summed over both axes: `|loop.d−Dt|/Dt + |loop.e−Et|/Et`; lower is better | Step 2 `Score` |
| Greedy de-dup | fill tiers in order, each skipping loops earlier tiers already took | Step 2 `BuildOptions` |
| Cold start | the no-last-week fallback to fixed seed targets | Step 2 `BuildTargets` |
| Elevation floor | `max(E_last, 50)` — so a flat week still asks for a little climbing | Step 2 |

---

## 2. Decisions already made (challenge them if they seem wrong)

| Decision | Choice | Why |
|---|---|---|
| Where the maths lives | A pure `ChallengeCalibrator` in **Domain** (mirrors `ChallengeCalculator`); the handler only gathers inputs and maps outputs | Same reason Stage 1 was pure: no DB/HTTP, so it's trivially unit-testable, and the game rule lives in one honest place. |
| Multipliers | Safe `0.90`, Step `1.10`, Reach `1.30` | Straight from the domain model's tunable defaults. |
| Elevation floor | `max(E_last, 50)` before applying multipliers | A flat week (`E_last ≈ 0`) would otherwise ask for ~0 m of climbing on all three. The floor keeps a little vertical in play. |
| Scoring | Relative closeness on both axes, summed; **lower wins**. The loop's `LoopTier` is **not** scored | Percentage closeness makes small and giant loops comparable. The difficulty band is descriptive only. |
| De-duplication | **Greedy Safe→Step→Reach, up to 3 distinct**; fewer if the library can't fill 3 | Your choice. Simple, deterministic, and satisfies the spec's "de-duplicate so the three differ." |
| Cold start | If **last-week distance ≤ 0**, use fixed seed targets `20 000/300`, `30 000/600`, `40 000/1 000` (m), then score normally | Domain model default ("fixed seed targets"). Triggering on zero distance also covers a total rest week and avoids dividing by a `0` target. |
| What an option shows | the calibration tier, the **calibrated target** (for context), and the **loop's real targets** (what selecting will snapshot) | The user picks a real loop; selecting it in Stage 4 snapshots the *loop's* numbers, not the calibrated aim — so the DTO shows both. |
| Last-week summing | reuse the Stage 5 in-memory pattern (pull user's activities → filter qualifying + in-week → sum) | Same correctness/clarity reasons as Stage 5; works identically on Postgres and SQLite. (It now appears twice — see the note in §5 Step 3 about extracting it later.) |
| New packages / DI / migration | **None.** Reuses `TimeProvider`, `ChallengeController`, `TestDb`, `FixedTimeProvider` | Pure read + pure domain; nothing to register or migrate. |

---

## 3. One-time setup

**Nothing to install or register.** You'll add one tiny domain helper (`Week.Previous()`), reuse the
Stage 4 clock/controller and the Stage 3/4 test helpers, and add a new Application slice — handlers are
auto-discovered by the assembly scan, so there's no DI line this stage.

---

## 4. The map — every new file

```
src/LoopQuest.Domain/
  Time/Week.cs                                                   🧩  ADD a one-line Previous()
  Challenges/ChallengeCalibrator.cs                              📖  (enum + record + the calculator)
src/LoopQuest.Application/
  Challenges/Queries/GetChallengeOptions/ChallengeOptionDto.cs            📖
  Challenges/Queries/GetChallengeOptions/GetChallengeOptionsQuery.cs      📖
  Challenges/Queries/GetChallengeOptions/GetChallengeOptionsQueryHandler.cs 📖 (+ FallsInWeek helper)
src/LoopQuest.Api/
  Controllers/ChallengeController.cs                             🧩  ADD a GET options method
tests/LoopQuest.Domain.Tests/
  ChallengeCalibratorTests.cs                                    📖 + 🧩  (the heart — cold start, scoring, de-dup, floor)
tests/LoopQuest.Infrastructure.Tests/
  Challenges/GetChallengeOptionsQueryHandlerTests.cs             📖 + 🧩  (last-week summing + Previous)
```

No migration — no schema change. (Another tell that you're building a pure read over a pure calculation.)

---

## 5. Build order

> Order: domain helper → domain calculator (+ its tests) → application slice → controller → handler
> tests. Build the pure logic first, prove it, then wire HTTP around it.

### Step 1 — Domain: `Week.Previous()` 🧩

**The plan, in plain words:** add the one missing piece of week arithmetic — "the week before this
one." **Where it's used:** the handler calls it in beat 2 to get last week's Monday–Sunday so it can
sum last week's runs.

Add this inside the `Week` struct (right under `Current`):

```csharp
/// <summary>The seven-day week immediately before this one.</summary>
public Week Previous() => ContainingDate(Start.AddDays(-1));
```

**Why it works:** `Start` is this week's Monday. `Start.AddDays(-1)` is the previous Sunday, and
`ContainingDate` of that Sunday is the previous Monday–Sunday week. You're reusing the off-by-one logic
you already got right, so there's nothing new to get wrong.

Worth a quick test in `WeekTests` (🧩): `Previous_OfAMonday_IsThePriorMonToSun` — e.g. the week starting
`2026-06-08` has `Previous().Start == 2026-06-01` and `Previous().End == 2026-06-07`.

### Step 2 — Domain: `ChallengeCalibrator` (the heart) 📖 + 🧩

**The plan, in plain words:** this is the brain of the stage — a pure function from *(last week's two
numbers, the loop library)* to *three calibrated options*. No database, no clock, no HTTP. **Where it's
used:** the handler hands it the numbers it gathered and gets back the three chosen options to map into
DTOs. It's also the thing your unit tests hammer directly.

> **TDD-kata option:** this is the Stage 6 equivalent of the Stage 1 calculator. If you'd rather learn
> it the way Stage 1 worked, write `ChallengeCalibratorTests` first (Step 2's tests below), watch them
> go red, and implement `BuildOptions` yourself — the reference is here whenever you want it. Otherwise,
> type it in and lean on the walkthrough.

Create `Challenges/ChallengeCalibrator.cs`:

```csharp
using LoopQuest.Domain.Entities;

namespace LoopQuest.Domain.Challenges;

/// <summary>The three escalating weekly options. NOTE: unrelated to <c>LoopTier</c> (a loop's
/// difficulty band) — these are the calibration buckets the app offers each week.</summary>
public enum CalibrationTier { Safe, Step, Reach }

/// <summary>One calibrated option: which bucket it is, the (distance, elevation) the algorithm aimed
/// for, and the real loop chosen as the closest fit.</summary>
public readonly record struct ChallengeOption(
    CalibrationTier Tier,
    double CalibratedDistanceMeters,
    double CalibratedElevationMeters,
    Loop Loop);

/// <summary>
/// Turns last week's totals and the active loop library into three escalating, distinct options.
/// Pure, dependency-free domain logic; the authoritative spec is docs/02-domain-model.md
/// ("Weekly challenge selection") plus ChallengeCalibratorTests.
/// </summary>
public static class ChallengeCalibrator
{
    public const double SafeMultiplier = 0.90;
    public const double StepMultiplier = 1.10;
    public const double ReachMultiplier = 1.30;

    /// <summary>Flat-week floor: even with ~0 m climbed last week, ask for a little vertical.</summary>
    public const double MinimumElevationMeters = 50.0;

    // Fixed seed targets (meters) used when there is no last week to calibrate from.
    private static readonly IReadOnlyList<(CalibrationTier Tier, double Distance, double Elevation)>
        ColdStartTargets =
        [
            (CalibrationTier.Safe,  20_000, 300),
            (CalibrationTier.Step,  30_000, 600),
            (CalibrationTier.Reach, 40_000, 1_000),
        ];

    public static IReadOnlyList<ChallengeOption> BuildOptions(
        double lastWeekDistanceMeters,
        double lastWeekElevationMeters,
        IReadOnlyList<Loop> activeLoops)
    {
        // 1. Decide the three (distance, elevation) targets to aim for.
        var targets = BuildTargets(lastWeekDistanceMeters, lastWeekElevationMeters);

        // 2. For each target IN ORDER, take the nearest loop not already used (greedy de-dup).
        var chosen = new List<ChallengeOption>(targets.Count);
        var used = new HashSet<Guid>();

        foreach (var (tier, targetDistance, targetElevation) in targets)
        {
            Loop? best = null;
            double bestScore = double.MaxValue;

            foreach (var loop in activeLoops)
            {
                if (used.Contains(loop.Id))
                {
                    continue;   // an earlier tier already took this loop — keep the three distinct
                }

                double score = Score(loop, targetDistance, targetElevation);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = loop;
                }
            }

            if (best is null)
            {
                break;   // library exhausted — return however many distinct options we could fill
            }

            used.Add(best.Id);
            chosen.Add(new ChallengeOption(tier, targetDistance, targetElevation, best));
        }

        return chosen;
    }

    // Lower is better. Relative (percentage) gap from the target on each axis, summed. Dividing by the
    // target is what makes a 20 km loop and a 160 km loop comparable.
    // 🧩 Try writing this one line yourself from the formula in the cheat sheet, then check it here.
    private static double Score(Loop loop, double targetDistance, double targetElevation) =>
        Math.Abs(loop.TargetDistanceMeters - targetDistance) / targetDistance +
        Math.Abs(loop.TargetElevationMeters - targetElevation) / targetElevation;

    private static IReadOnlyList<(CalibrationTier Tier, double Distance, double Elevation)> BuildTargets(
        double lastWeekDistanceMeters, double lastWeekElevationMeters)
    {
        // Cold start: nothing to calibrate from (new user, or a complete rest week). Seed targets keep
        // the first options sensible — and avoid a 0 target (which would divide by zero in Score).
        if (lastWeekDistanceMeters <= 0)
        {
            return ColdStartTargets;
        }

        // Floor the elevation so a flat week still asks for a little climbing.
        double elevationBase = Math.Max(lastWeekElevationMeters, MinimumElevationMeters);

        return
        [
            (CalibrationTier.Safe,  lastWeekDistanceMeters * SafeMultiplier,  elevationBase * SafeMultiplier),
            (CalibrationTier.Step,  lastWeekDistanceMeters * StepMultiplier,  elevationBase * StepMultiplier),
            (CalibrationTier.Reach, lastWeekDistanceMeters * ReachMultiplier, elevationBase * ReachMultiplier),
        ];
    }
}
```

**Walkthrough — the parts that are new or easy to get wrong:**

- **Two collaborating helpers.** `BuildTargets` decides *what to aim for* (last-week-scaled, or cold-start
  seeds). `BuildOptions` then *matches a loop* to each aim. Splitting them keeps each readable and lets a
  test check the targets and the matching separately.
- **The greedy loop is the de-dup.** The `used` `HashSet<Guid>` is the whole trick: before scoring, skip
  any loop an earlier tier already claimed. Because we iterate Safe → Step → Reach, Safe gets first pick,
  then Step, then Reach — deterministic and distinct.
- **`break` on `best is null`** means "no unused loop left." With the 9 seeded loops you'll always get 3,
  but a tiny library returns fewer rather than repeating — exactly the behaviour you chose.
- **`Score` divides by the target (your 🧩).** This is the one formula of the stage and it's straight from
  the spec. Dividing by `targetDistance`/`targetElevation` turns raw metres into a percentage gap, so a
  small urban loop and a giant ultra are judged fairly. The targets are always `> 0` here (cold-start
  seeds are positive; the calibrated path only runs when `lastWeekDistance > 0`, and the elevation floor
  keeps `Et ≥ 45`), so there's no divide-by-zero — *as long as you keep the cold-start guard and the
  floor*.

**Questions you'll probably ask:**

- *"Why does the calculator take `Loop` entities, not DTOs?"* — `Loop` is a Domain type, so a Domain
  calculator may use it freely, and it keeps the function pure (it receives a list, returns picks; no DB).
  The Application layer does the DTO mapping afterwards.
- *"Why is `ChallengeOption` a `record struct`?"* — it's a small immutable value (a tier, two numbers, a
  loop reference); value semantics fit, exactly like `ChallengeProgress` in Stage 1.
- *"Could the loop's difficulty `Tier` ever matter?"* — not in v1's algorithm. It's display-only. If you
  ever want "one Easy, one Hard, one Epic," that's a different rule — out of scope (§9).

**Step 2 tests — `ChallengeCalibratorTests`** (📖 one worked example; the rest 🧩). Mirror the
`ChallengeCalculatorTests` style (xUnit `Assert`, doubles to 2 dp):

```csharp
using LoopQuest.Domain.Challenges;
using LoopQuest.Domain.Entities;
using LoopQuest.Domain.Enums;

namespace LoopQuest.Domain.Tests;

public class ChallengeCalibratorTests
{
    private const int Precision = 2;

    // A small spread of loops to choose from (distance m / elevation m).
    private static IReadOnlyList<Loop> SampleLibrary() =>
    [
        Loop.Create("Flat 20",  "d", LoopCategory.Urban,   LoopTier.Easy,     20_000, 300),
        Loop.Create("Rolling 30","d", LoopCategory.Urban,  LoopTier.Moderate, 30_000, 600),
        Loop.Create("Hilly 40", "d", LoopCategory.Classic, LoopTier.Hard,     40_000, 1_000),
        Loop.Create("Alpine 60","d", LoopCategory.Fantasy, LoopTier.Hard,     60_000, 3_000),
    ];

    [Fact]
    public void ColdStart_UsesSeedTargets_AndReturnsThreeDistinctLoops()
    {
        var options = ChallengeCalibrator.BuildOptions(
            lastWeekDistanceMeters: 0, lastWeekElevationMeters: 0, activeLoops: SampleLibrary());

        Assert.Equal(3, options.Count);
        Assert.Equal(
            new[] { CalibrationTier.Safe, CalibrationTier.Step, CalibrationTier.Reach },
            options.Select(o => o.Tier).ToArray());
        Assert.Equal(3, options.Select(o => o.Loop.Id).Distinct().Count());     // all different
        Assert.Equal(20_000, options[0].CalibratedDistanceMeters, Precision);   // seed Safe distance
        Assert.Equal(40_000, options[2].CalibratedDistanceMeters, Precision);   // seed Reach distance
    }

    // 🧩 Calibrated_PicksNearestLoopPerTier
    //     last week 40 km / 1000 m → Safe 36k/900, Step 44k/1100, Reach 52k/1300.
    //     Assert the chosen loop names are the nearest to each (work the scores out by hand once).
    //
    // 🧩 SameNearestLoop_IsNotRepeated_GreedyKeepsThemDistinct
    //     build a library where one loop is nearest for two tiers; assert 3 distinct ids, Safe keeps it.
    //
    // 🧩 FlatWeek_FloorsElevationTargetAt50
    //     last week 40 km / 0 m → Safe elevation target = max(0,50)*0.90 = 45 (assert via CalibratedElevationMeters).
    //
    // 🧩 LibrarySmallerThanThree_ReturnsFewer
    //     pass two loops; assert options.Count == 2 and the two are distinct.
}
```

**What the worked test pins:** cold start ignores the (zero) last-week numbers, uses the seed targets,
and still returns three *distinct* loops in Safe/Step/Reach order. The two `CalibratedDistanceMeters`
asserts prove the seed path ran instead of the multiplier path.

**Checkpoint:** `dotnet test tests/LoopQuest.Domain.Tests` green.

### Step 3 — Application: the slice (DTO → Query → Handler) 📖

**The plan, in plain words:** wrap the pure calculator in a use case that fetches its inputs from the
database and shapes its output for the API. **Where it's used:** the controller sends the query; the
handler runs; the DTO list becomes the JSON the picker screen renders.

**(a) `ChallengeOptionDto`** — the safe projection:

```csharp
namespace LoopQuest.Application.Challenges.Queries.GetChallengeOptions;

/// <summary>
/// One calibrated option for the weekly picker. <see cref="Tier"/> is the calibration bucket
/// (Safe/Step/Reach); <see cref="LoopTier"/> is the loop's difficulty band (Easy..Epic) — two different
/// things. The <c>Calibrated…</c> numbers are what the algorithm aimed for; the <c>Target…</c> numbers
/// are the loop's real targets — the ones Stage 4 will snapshot if this option is selected.
/// </summary>
public sealed record ChallengeOptionDto(
    string Tier,
    double CalibratedDistanceMeters,
    double CalibratedElevationMeters,
    Guid LoopId,
    string LoopName,
    string LoopCategory,
    string LoopTier,
    double TargetDistanceMeters,
    double TargetElevationMeters);
```

**(b) `GetChallengeOptionsQuery`:**

```csharp
using MediatR;

namespace LoopQuest.Application.Challenges.Queries.GetChallengeOptions;

/// <summary>Query: the three calibrated options for this week, based on last week. No parameters in v1.</summary>
public sealed record GetChallengeOptionsQuery : IRequest<IReadOnlyList<ChallengeOptionDto>>;
```

**(c) `GetChallengeOptionsQueryHandler`** — gather inputs, call the calculator, map out:

```csharp
using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Domain.Activities;
using LoopQuest.Domain.Challenges;
using LoopQuest.Domain.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Application.Challenges.Queries.GetChallengeOptions;

/// <summary>
/// Builds the three calibrated weekly options: sum last week's qualifying activities, then run the
/// totals and the active loop library through <see cref="ChallengeCalibrator"/>. Pure read: no writes.
/// </summary>
public sealed class GetChallengeOptionsQueryHandler(IAppDbContext db, TimeProvider clock)
    : IRequestHandler<GetChallengeOptionsQuery, IReadOnlyList<ChallengeOptionDto>>
{
    public async Task<IReadOnlyList<ChallengeOptionDto>> Handle(
        GetChallengeOptionsQuery request, CancellationToken cancellationToken)
    {
        // 1. The single v1 athlete — for the time zone.
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("No user yet — connect Strava first.");
        }

        // 2. LAST week's Mon..Sun, in the user's local time (note .Previous()).
        var lastWeek = Week.Current(user.TimeZoneId, clock.GetUtcNow()).Previous();

        // 3. Sum last week's qualifying activities (same in-memory pattern as Stage 5).
        var activities = await db.Activities
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var lastWeekActivities = activities.Where(a =>
            QualifyingSportTypes.Includes(a.SportType) &&
            FallsInWeek(a.StartDateLocal, lastWeek.Start, lastWeek.End));

        double lastWeekDistance = lastWeekActivities.Sum(a => a.DistanceMeters);
        double lastWeekElevation = lastWeekActivities.Sum(a => a.ElevationGainMeters);

        // 4. The pool the calibrator scores: the active library.
        var loops = await db.Loops
            .AsNoTracking()
            .Where(l => l.IsActive)
            .ToListAsync(cancellationToken);

        // 5. The pure domain rule turns numbers + library into three calibrated options.
        var options = ChallengeCalibrator.BuildOptions(lastWeekDistance, lastWeekElevation, loops);

        // 6. Project to DTOs (never return the entity).
        return options.Select(o => new ChallengeOptionDto(
            Tier: o.Tier.ToString(),
            CalibratedDistanceMeters: o.CalibratedDistanceMeters,
            CalibratedElevationMeters: o.CalibratedElevationMeters,
            LoopId: o.Loop.Id,
            LoopName: o.Loop.Name,
            LoopCategory: o.Loop.Category.ToString(),
            LoopTier: o.Loop.Tier.ToString(),
            TargetDistanceMeters: o.Loop.TargetDistanceMeters,
            TargetElevationMeters: o.Loop.TargetElevationMeters)).ToList();
    }

    // Identical to Stage 5's helper: an activity counts if its LOCAL date is within Mon..Sun inclusive.
    private static bool FallsInWeek(DateTimeOffset localStart, DateOnly weekStart, DateOnly weekEnd)
    {
        var date = DateOnly.FromDateTime(localStart.DateTime);
        return date >= weekStart && date <= weekEnd;
    }
}
```

**Walkthrough — the parts that are new or easy to get wrong:**

- **Beat 2 is the one-word difference from Stage 5: `.Previous()`.** Everything else about summing a week
  is the same; the only change is *which* week. Get this wrong (drop `.Previous()`) and you calibrate off
  the half-finished current week — a bug nothing will flag.
- **Beat 3 reuses the Stage 5 pattern verbatim**, including the `FallsInWeek` helper. That helper now
  exists in two handlers. That's fine for v1, but it's a fair candidate to extract into a small shared
  helper (e.g. a pure `WeeklyTotals` in Domain that both Stage 5 and Stage 6 call) once you're tired of
  the copy — left separate here so each stage stands alone.
- **Beat 5 is a plain function call.** The handler does no maths itself; all the calibration lives in the
  tested Domain class. The handler's only jobs are *fetch* and *map*.
- **Beat 6 maps both number pairs.** `Calibrated…` (what we aimed for) and `Target…` (the loop's real
  goal). The picker can show "we suggested ~44 km; nearest is Zermatt Marathon, 42 km / 1 850 m," and
  selecting snapshots the **42 km / 1 850 m**, never the 44.

**Checkpoint:** `dotnet build` clean.

### Step 4 — Api: add the GET to `ChallengeController` 🧩

**The plan, in plain words:** expose the query over HTTP. **Where it's used:** the picker screen calls
`GET /api/challenge/options` to render the three cards. You're adding a method to the controller Stage 4
already created.

```csharp
/// <summary>The three calibrated options for this week, based on last week's training.</summary>
[HttpGet("options")]
[ProducesResponseType(typeof(IReadOnlyList<ChallengeOptionDto>), StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<ChallengeOptionDto>>> Options(CancellationToken cancellationToken)
    => Ok(await sender.Send(new GetChallengeOptionsQuery(), cancellationToken));
```

Add `using LoopQuest.Application.Challenges.Queries.GetChallengeOptions;`. An empty library (shouldn't
happen — it's seeded) would return `[]`, which is honest.

**Checkpoint:** `dotnet build` clean; endpoint visible in Scalar.

### Step 5 — Tests: the handler proves the wiring 📖 + 🧩

**The plan, in plain words:** the Domain tests already prove the *maths*; these prove the *plumbing* —
that the handler sums **last** week (not this week) and feeds the calibrator real loops. **Where it's
used:** `dotnet test`. Reuse Stage 3's `TestDb` and Stage 4's `FixedTimeProvider`.

```csharp
using LoopQuest.Application.Challenges.Queries.GetChallengeOptions;
using LoopQuest.Domain.Entities;
using LoopQuest.Domain.Enums;
using LoopQuest.Infrastructure.Persistence;
using LoopQuest.Infrastructure.Tests.Sync;   // TestDb
using Microsoft.Data.Sqlite;

namespace LoopQuest.Infrastructure.Tests.Challenges;

public sealed class GetChallengeOptionsQueryHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SqliteConnection _connection;
    // Saturday 2026-06-13 → this week Mon 06-08..Sun 06-14; LAST week Mon 06-01..Sun 06-07.
    private readonly FixedTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero));

    public GetChallengeOptionsQueryHandlerTests() => (_db, _connection) = TestDb.Create();
    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    [Fact]
    public async Task Options_CalibrateOffLastWeek_NotThisWeek()
    {
        var user = User.Create(67890, "Viv M");
        _db.Users.Add(user);
        _db.Loops.AddRange(
            Loop.Create("Flat 20", "d", LoopCategory.Urban, LoopTier.Easy, 20_000, 300),
            Loop.Create("Mid 30",  "d", LoopCategory.Urban, LoopTier.Moderate, 30_000, 600),
            Loop.Create("Big 40",  "d", LoopCategory.Classic, LoopTier.Hard, 40_000, 1_000));

        // A run LAST week (counts) and a run THIS week (must be ignored by options).
        _db.Activities.Add(Activity.Create(user.Id, 1, "Last", "Run",
            new DateTimeOffset(2026, 6, 3, 7, 0, 0, TimeSpan.Zero), 30_000, 600));
        _db.Activities.Add(Activity.Create(user.Id, 2, "This", "Run",
            new DateTimeOffset(2026, 6, 9, 7, 0, 0, TimeSpan.Zero), 99_000, 9_000));
        await _db.SaveChangesAsync();

        var result = await new GetChallengeOptionsQueryHandler(_db, _clock)
            .Handle(new GetChallengeOptionsQuery(), CancellationToken.None);

        Assert.Equal(3, result.Count);
        // Safe target = 30 km * 0.90 = 27 km — proves it used last week's 30 km, not this week's 99 km.
        Assert.Equal(27_000, result[0].CalibratedDistanceMeters, 2);
        Assert.Equal(3, result.Select(o => o.LoopId).Distinct().Count());
    }

    // 🧩 Options_NoActivitiesAtAll_FallsBackToColdStartSeeds   (no activities → Safe calibrated distance == 20 000)
    // 🧩 Options_NoUser_Throws                                 (empty db → InvalidOperationException)
}
```

**What the worked test pins:** the two activities are the trap — one last week, one this week. The
`27_000` assert (30 km × 0.90) can only pass if the handler summed **last** week and ignored this week's
99 km. That's the `.Previous()` correctness, proven end-to-end through real EF.

**Checkpoint:** `dotnet test` — everything green.

### Step 6 — The moment of truth 🤝

1. AppHost running → open Scalar.
2. **Cold start first:** with no activities in the DB, `GET /api/challenge/options` → three options whose
   `calibratedDistanceMeters` are `20000 / 30000 / 40000` (the seeds), each a distinct loop.
3. **Then simulate last week:** use Stage 5's dev seeder (Step 6 there), but date the fake activities in
   **last** week (the prior Mon–Sun). Re-call options → the calibrated targets now scale off those totals
   (Safe = last-week distance × 0.90, etc.), and the chosen loops shift to fit.
4. Sanity-check the escalation: `Safe.calibratedDistanceMeters < Step… < Reach…`, and the three `loopId`s
   are distinct.

---

## 6. Calibration cheat sheet

```
D_last = last week's total qualifying distance (m)        ← Week.Current(tz, now).Previous()
E_last = last week's total qualifying elevation (m)
E_base = max(E_last, 50)                                  ← flat-week floor

Targets (cold start when D_last <= 0 → fixed seeds 20k/300, 30k/600, 40k/1000):
  Safe  -> D_last * 0.90 , E_base * 0.90
  Step  -> D_last * 1.10 , E_base * 1.10
  Reach -> D_last * 1.30 , E_base * 1.30

Score a loop against a target (lower = better):
  score = |loop.distance - Dt| / Dt  +  |loop.elevation - Et| / Et

Fill Safe -> Step -> Reach, each taking the nearest loop NOT already taken (greedy, up to 3 distinct).
```

| | |
|---|---|
| "Tier" — two meanings | **CalibrationTier** = Safe/Step/Reach (this stage). **LoopTier** = Easy..Epic (difficulty, not scored). |
| Which week | **last** week — `.Previous()`. Summing this week is the classic bug. |
| Closeness | **relative** (÷ target), summed over both axes. |
| Cold start | `D_last <= 0` → fixed seed targets (also avoids ÷0). |
| What selection snapshots | the **loop's real targets**, not the calibrated aim. |

---

## 7. Pitfalls (each is a real bug waiting)

> As before: none of these are compile errors. They build clean and lie at runtime.

1. **Scoring on the loop's difficulty `Tier`.** The maths uses distance and elevation only. `LoopTier`
   (Easy..Epic) is descriptive; never feed it into the score.
2. **Calibrating off this week.** Use `.Previous()`. Without it you scale off a week still in progress —
   the worked handler test exists to catch exactly this.
3. **Dropping the cold-start guard.** If `D_last == 0` and you skip the seed fallback, all three targets
   are `0` and `Score` divides by zero. The `lastWeekDistanceMeters <= 0` check is load-bearing.
4. **Dropping the elevation floor.** A flat week makes `E_last == 0`; without `max(…, 50)` the elevation
   half of every score divides by zero (or by a near-zero target). Keep the floor.
5. **Absolute instead of relative closeness.** Forgetting to divide by the target makes big loops always
   "far" and a small loop always wins. Divide by `Dt` and `Et`.
6. **Forgetting the `used` set.** Without it, one loop can win multiple tiers and you return duplicates —
   the opposite of the chosen "3 distinct" behaviour.
7. **Showing the calibrated target as the goal.** The DTO carries both, but selection (Stage 4) snapshots
   the **loop's** real targets. Don't wire the calibrated number into selection.
8. **Greedy order drift.** Iterate Safe → Step → Reach so results are deterministic; shuffling the order
   changes which tier gets first pick of a contested loop.

---

## 8. Definition of Done

- [ ] `dotnet build` — no new warnings/errors
- [ ] `dotnet test tests/LoopQuest.Domain.Tests` green: cold start (seeds + 3 distinct), nearest-per-tier
      scoring, greedy de-dup keeps them distinct, flat-week elevation floor, small-library returns fewer
- [ ] `dotnet test` (handler) green: calibrates off **last** week (not this week); no-user throws;
      no-activities falls back to seeds
- [ ] `GET /api/challenge/options` returns up to **3 distinct**, escalating options (Safe/Step/Reach),
      each carrying the calibrated target and the loop's real targets
- [ ] No migration; no new package references; dependency rule intact (calibration is pure Domain)
- [ ] New code carries light comments (`///` on public types, a `//` only where it isn't obvious)
- [ ] Reviewed, then merged: `git switch dev && git merge --no-ff feature/stage-6-challenge-options`

## 9. Explicitly out of scope (resist the urge)

- **Selecting an option** — that's Stage 4's `POST /api/challenge/select`. Options only *suggests*; the
  user still posts the `loopId` they chose.
- **Marking which option (if any) is already active this week / hiding taken loops** — a later UX polish.
- **Config-driven multipliers / seeds** — hard-coded consts in v1; promoting them to configuration is a
  clean later change.
- **Smarter assignment** (globally optimal loop↔tier matching) — greedy Safe-first is enough for v1.
- **Using a loop's difficulty `Tier` in the algorithm** (e.g. "one Easy, one Hard, one Epic") — a
  different rule; deliberately not built.
- **Extracting the shared last-week/this-week summing helper** — fair refactor once it's duplicated;
  left as-is so Stages 5 and 6 read independently.
