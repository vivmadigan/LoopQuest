# Stage 5 Plan — Live Progress for the Current Challenge

> **How to use this doc:** work top to bottom. Every task is tagged:
> **🧩 you write it** · **📖 reference example — type it, adapt it, understand it** · **🤝 we do it together in chat**.
> Drafted by Claude (2026-06-23). This plan deliberately has **more worked-through code and more
> explanation** than the Stage 4 plan, and every step opens with a plain-words *"what this is and where
> the code gets used"* so you always know why you're typing what you're typing.
>
> **Vocabulary reminder:** a *Stage* is a roadmap chapter (this doc is Stage 5); a *Step* is a numbered
> task inside it. Say which one you mean in chat and nobody gets lost.
>
> **When a step feels foggy**, the rescue order is: ① that step's *"the plan, in plain words"* opener
> ② its *"what each line does"* walkthrough ③ the domain model's [Progress & completion](../02-domain-model.md#progress--completion)
> and [The week](../02-domain-model.md#the-week) sections ④ ask Claude *"what do I actually type?"* —
> a normal question, not a failure.
>
> **Dependencies:** this stage is the payoff of three earlier ones — Stage 1 (the calculator), Stage 3
> (synced activities), Stage 4 (the challenge + week math). All three are merged, so:
> `git switch dev && git switch -c feature/stage-5-live-progress`

---

## 1. What you're building

`GET /api/challenge/current` answers one question: **"How am I doing on this week's challenge right
now?"** It reads your active challenge, sums this week's qualifying runs, runs them through the Stage 1
rule, and returns the numbers the two progress bars will draw.

```
GET /api/challenge/current
   │
   ▼
GetCurrentChallengeQuery        ← no input, so NO validator (unlike Stage 4's command)
   │
   ▼
GetCurrentChallengeQueryHandler (Application)   ctor: (IAppDbContext db, TimeProvider clock)
   │ 1. load the single user                    (we need its TimeZoneId)
   │ 2. week = Week.Current(user.TimeZoneId, clock.GetUtcNow())   ← which week is it, locally?
   │ 3. find THIS week's Active challenge        (none yet? → return null, a normal state)
   │ 4. read the loop's NAME live                (display only; the goal NUMBERS are the snapshot)
   │ 5. pull this user's activities, then in memory:
   │        keep qualifying sport types AND activities whose local date is in the week
   │        sum distance, sum elevation
   │ 6. ChallengeCalculator.Calculate(snapshot targets + the two sums)   ← reuse Stage 1
   │ 7. project everything into CurrentChallengeDto
   ▼
CurrentChallengeDto  (targets, achieved, effective elevation, purchased, 3 completion flags)
   │  OR null when there is no challenge this week
```

**Why this stage matters:** Stages 1, 3 and 4 each built one piece in isolation. Stage 5 is the first
time they *talk to each other* — the synced activities (Stage 3) meet the frozen goal (Stage 4) through
the game rule (Stage 1). Notice what's **not** here: no new domain maths, no database writes. Stage 5 is
a **read** that wires existing parts together. That's the whole character of this stage, and it's why
it's a good one to build your confidence with queries.

### The complexities of this stage, in plain words

1. **It's a Query, not a Command.** It only *reads*. So: no validator (there's no input to check), no
   `SaveChanges`, and we tag the database reads `AsNoTracking()` (we're not going to modify these rows,
   so EF needn't keep change-tracking bookkeeping for them — it's a small, free speed-up).
2. **Picking the *right* challenge.** "Current" means *this week's* Active challenge — not just "any
   Active one." Until Stage 7 builds the weekly rollover, last week's challenge can still be sitting at
   `Active`. So we compute this week with the clock and match on `WeekStart`. This is the **same injected-clock
   lesson from Stage 4**, and here's the proof it still matters even for a read: without it, a brand-new
   week (before you've picked anything) would wrongly show *last* week's challenge as current.
3. **Matching activities to the week.** An activity belongs to the week if its **local date** lands in
   `WeekStart..WeekEnd`. The good news (you'll see it in the cheat sheet): `Activity.StartDateLocal`
   already holds the *local* wall-clock time — Strava converted it for us — so "the activity's local
   date" is just `DateOnly.FromDateTime(startDateLocal.DateTime)`. No timezone maths in this stage.
4. **Reusing the calculator.** The summed distance/elevation plus the challenge's **snapshot** targets
   go straight into `ChallengeCalculator.Calculate`. You don't re-derive the substitution rule — you
   feed Stage 1 its four numbers and read the result.
5. **The empty state.** No challenge picked yet is normal, not an error, so the endpoint returns **200
   with a `null` body** rather than a 404 (see §2 for the reasoning).

### New words in this stage

| Word | What it actually is | You'll meet it in |
|---|---|---|
| Query (CQRS read side) | a MediatR request that *reads and returns* data and never changes the database. Same `IRequest<T>` machinery as a command; the Queries/ vs Commands/ folders are the only thing separating them | Step 2 |
| `AsNoTracking()` | "I'm only reading these rows, don't bother tracking them for changes." A read-only optimisation | Step 3 handler |
| Projection | turning entity rows into a DTO with `.Select(...)`, so the API never leaks the EF entity | Step 1 DTO · Step 3 |
| In-memory filtering | pulling rows into the app with `ToListAsync()` and then filtering/summing in C# (vs. asking the database to do it in SQL) | Step 3 beat 5 |
| Nullable result (`Dto?`) | the handler can legitimately return "nothing" (`null`) — the type says so out loud | Step 1 · Step 3 |
| Effective elevation | real elevation **plus** the capped amount bought with surplus distance — the Stage 1 output the elevation bar actually draws | Step 1 DTO |

---

## 2. Decisions already made (challenge them if they seem wrong)

| Decision | Choice | Why |
|---|---|---|
| Empty state (no challenge this week) | **200 OK with a `null` body** (handler returns `CurrentChallengeDto?`) | "Haven't picked yet" is a normal state, not an error. A 404 turns routine start-of-week requests into error-looking logs; an envelope (`{hasChallenge:false}`) adds a wrapper type for no gain. `Ok(result)` stays trivial and the frontend just checks for null. |
| Which challenge is "current" | *This week's* `Active` challenge, found with the **injected clock** (`Week.Current` → match `WeekStart`) | Before Stage 7's rollover, an old challenge can linger at `Active`. Matching on this week's Monday is the correct, unambiguous pick. |
| Where filtering + summing happens | **In memory**: pull this user's activities, then filter (qualifying sport + in-week) and sum in C# | Provider-independent (the same code is correct on Postgres in prod and SQLite in tests), and it reuses the `QualifyingSportTypes` domain helper directly. For single-user v1 the row count is tiny. Pushing the filter into SQL is a clean later optimisation (§9). |
| Loop name | **Looked up live** from the loop; the target **numbers** come from the challenge snapshot | The callback to Stage 4: snapshot the *goal* (numbers), but a display label like the name can safely follow the live loop. |
| Read-only | `AsNoTracking()` on the reads, no `SaveChanges` | It's a query; we change nothing. |
| DTO shape | one flat `record` carrying targets, achieved, effective elevation, purchased, and the three completion flags | Everything the two bars need in one object; no nested types to start. |
| New use-case ingredients | reuse Stage 4's `TimeProvider` DI and `ChallengeController`; reuse Stage 1's `ChallengeCalculator` | Nothing new to register. Stage 5 adds a method to the existing controller and a new Application slice. |
| Tests | reuse Stage 3's `TestDb` (SQLite) + Stage 4's `FixedTimeProvider` | Real EF SQL, no Docker, time pinned. |
| New packages | **None.** | Keeps the dependency rule and CPM list unchanged. |

---

## 3. One-time setup

**Nothing to install, and nothing new to register.** Stage 4 already:

- added `DbSet<WeeklyChallenge> WeeklyChallenges` to `IAppDbContext` and `AppDbContext`,
- registered `TimeProvider.System` in `Application/DependencyInjection.cs`,
- created `ChallengeController` (you'll add one method to it),
- created `FixedTimeProvider` in the test project (you'll reuse it).

If any of those drifted, fix that first — Stage 5 leans on all four.

---

## 4. The map — every new file

```
src/LoopQuest.Application/
  Challenges/Queries/GetCurrentChallenge/CurrentChallengeDto.cs            📖
  Challenges/Queries/GetCurrentChallenge/GetCurrentChallengeQuery.cs       📖
  Challenges/Queries/GetCurrentChallenge/GetCurrentChallengeQueryHandler.cs 📖 (+ 🧩 one helper)
src/LoopQuest.Api/
  Controllers/ChallengeController.cs                                       🧩  (ADD a GET method to the existing file)
tests/LoopQuest.Infrastructure.Tests/
  Challenges/GetCurrentChallengeQueryHandlerTests.cs                       📖 + 🧩
```

No migration this stage — we add no columns and change no schema. (That's a nice tell that you're
building a pure read.)

---

## 5. Build order

> The order matters: each file you add makes the *next* one compile. DTO → Query → Handler → Controller
> → Tests.

### Step 1 — Application: the DTO (the shape of the answer) 📖

**The plan, in plain words:** before writing logic, decide *what the endpoint returns*. This DTO is that
answer — a flat bag of numbers and flags. **Where it's used:** the handler builds one and returns it;
the controller serialises it to JSON; the Stage 10 frontend reads it to draw the two bars.

Create `Challenges/Queries/GetCurrentChallenge/CurrentChallengeDto.cs`:

```csharp
namespace LoopQuest.Application.Challenges.Queries.GetCurrentChallenge;

/// <summary>
/// Live progress for the active challenge — everything the two progress bars need, in meters.
/// Targets are the challenge's frozen snapshot; achieved/effective/purchased and the flags come from
/// running this week's activity totals through <c>ChallengeCalculator</c>.
/// </summary>
public sealed record CurrentChallengeDto(
    Guid ChallengeId,
    string LoopName,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string Status,
    double TargetDistanceMeters,
    double TargetElevationMeters,
    double AchievedDistanceMeters,
    double AchievedElevationMeters,
    double EffectiveElevationMeters,
    double ElevationPurchasedMeters,
    bool DistanceComplete,
    bool ElevationComplete,
    bool ChallengeComplete);
```

**What each field is for:**

- `TargetDistanceMeters` / `TargetElevationMeters` — the **goal**, copied from the challenge snapshot (not the live loop).
- `AchievedDistanceMeters` / `AchievedElevationMeters` — the raw sums of this week's qualifying runs.
- `EffectiveElevationMeters` — real elevation **+** elevation bought with surplus distance (the Stage 1 output the elevation bar fills to).
- `ElevationPurchasedMeters` — how much of that was *bought* (the frontend draws this slice in a different shade).
- the three `…Complete` flags — straight from the calculator; `ChallengeComplete` is the "conquered it?" answer.

Why a flat record and not the entity? Same house rule as every slice: **never return an EF entity**; a
DTO keeps the JSON contract independent of the table and lets us expose `Status` as a readable string.

### Step 2 — Application: the Query 📖

**The plan, in plain words:** the Query is the *request message* — the thing the controller hands to
MediatR to say "run the get-current-challenge use case." It carries no data because the endpoint needs
no input (the user is the single v1 athlete; the week comes from the clock). **Where it's used:** the
controller does `sender.Send(new GetCurrentChallengeQuery())`; MediatR finds the matching handler.

Create `GetCurrentChallengeQuery.cs`:

```csharp
using MediatR;

namespace LoopQuest.Application.Challenges.Queries.GetCurrentChallenge;

/// <summary>
/// Query: live progress for the user's active challenge this week. No parameters in v1.
/// Returns <c>null</c> when there is no active challenge — a normal "you haven't picked yet" state.
/// </summary>
public sealed record GetCurrentChallengeQuery : IRequest<CurrentChallengeDto?>;
```

**The one thing worth noticing:** the result type is `CurrentChallengeDto?` — the `?` is the empty-state
decision from §2 made *visible in the type*. Anyone reading this line knows the answer can legitimately
be "nothing," so they're forced to handle it. That's the difference between a Query and Stage 4's
Command: the command always produced a challenge or threw; this query has a real "nothing here" answer.

### Step 3 — Application: the Handler (the centrepiece) 📖 + 🧩

**The plan, in plain words:** this is where the work happens. It finds the right challenge, sums the
right activities, runs the Stage 1 rule, and packs the result into the DTO. **Where it's used:** MediatR
calls it when the controller sends the query; it's the only place these four ingredients (user, challenge,
activities, calculator) come together.

Its constructor asks for two things — the database and the clock — exactly like a Stage 4 handler:

Create `GetCurrentChallengeQueryHandler.cs`:

```csharp
using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Domain.Activities;
using LoopQuest.Domain.Challenges;
using LoopQuest.Domain.Enums;
using LoopQuest.Domain.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Application.Challenges.Queries.GetCurrentChallenge;

/// <summary>
/// Reads the user's active challenge for the current week, sums this week's qualifying activities, and
/// runs them through <see cref="ChallengeCalculator"/> to produce live progress. Pure read: no writes.
/// </summary>
public sealed class GetCurrentChallengeQueryHandler(IAppDbContext db, TimeProvider clock)
    : IRequestHandler<GetCurrentChallengeQuery, CurrentChallengeDto?>
{
    public async Task<CurrentChallengeDto?> Handle(
        GetCurrentChallengeQuery request, CancellationToken cancellationToken)
    {
        // 1. The single v1 athlete. We need its TimeZoneId to know which week "now" falls in.
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("No user yet — connect Strava first.");
        }

        // 2. Which week is it, in the user's local time? Same injected-clock trick as Stage 4.
        var week = Week.Current(user.TimeZoneId, clock.GetUtcNow());

        // 3. This week's Active challenge. None? That's the normal "not picked yet" state → null.
        var challenge = await db.WeeklyChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.UserId == user.Id
                  && c.WeekStart == week.Start
                  && c.Status == ChallengeStatus.Active,
                cancellationToken);
        if (challenge is null)
        {
            return null;
        }

        // 4. The loop's NAME, looked up live (display only). The goal NUMBERS are the snapshot on the
        //    challenge — we never read targets off the live loop.
        var loopName = await db.Loops
            .Where(l => l.Id == challenge.LoopId)
            .Select(l => l.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "(unknown loop)";

        // 5. Pull this user's activities, then apply the GAME filters in memory: qualifying sport type
        //    AND a local date inside the challenge's week. Then sum the two columns.
        var activities = await db.Activities
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var weekActivities = activities.Where(a =>
            QualifyingSportTypes.Includes(a.SportType) &&
            FallsInWeek(a.StartDateLocal, challenge.WeekStart, challenge.WeekEnd));

        var achievedDistance = weekActivities.Sum(a => a.DistanceMeters);
        var achievedElevation = weekActivities.Sum(a => a.ElevationGainMeters);

        // 6. Reuse the Stage 1 rule. The snapshot targets are the goal; the sums are the progress.
        var progress = ChallengeCalculator.Calculate(new ChallengeProgressInput(
            challenge.SnapshotTargetDistanceMeters,
            challenge.SnapshotTargetElevationMeters,
            achievedDistance,
            achievedElevation));

        // 7. Project everything into the DTO (never return the entity).
        return new CurrentChallengeDto(
            ChallengeId: challenge.Id,
            LoopName: loopName,
            WeekStart: challenge.WeekStart,
            WeekEnd: challenge.WeekEnd,
            Status: challenge.Status.ToString(),
            TargetDistanceMeters: challenge.SnapshotTargetDistanceMeters,
            TargetElevationMeters: challenge.SnapshotTargetElevationMeters,
            AchievedDistanceMeters: achievedDistance,
            AchievedElevationMeters: achievedElevation,
            EffectiveElevationMeters: progress.EffectiveElevationMeters,
            ElevationPurchasedMeters: progress.ElevationPurchasedMeters,
            DistanceComplete: progress.DistanceComplete,
            ElevationComplete: progress.ElevationComplete,
            ChallengeComplete: progress.ChallengeComplete);
    }

    // 🧩 Try writing this one yourself first — it's the only logic in the handler.
    // An activity belongs to the week if its LOCAL date is within Mon..Sun (inclusive).
    // StartDateLocal already holds local wall-clock time, so .DateTime → DateOnly is the local date.
    private static bool FallsInWeek(DateTimeOffset localStart, DateOnly weekStart, DateOnly weekEnd)
    {
        var date = DateOnly.FromDateTime(localStart.DateTime);
        return date >= weekStart && date <= weekEnd;
    }
}
```

**Walkthrough — the parts that are new or easy to get wrong:**

- **Beat 1 throws, beat 3 returns null — and that difference is deliberate.** "No user at all" is a
  broken setup (you haven't connected Strava), so it's an exception. "No challenge this week" is an
  expected everyday state, so it's a `null` result. Same shape of check (`is null`), opposite meaning —
  read each one and ask "is this a bug or a normal Tuesday?"
- **Beat 2 needs the clock even though nothing is being written.** This is the reinforcement of the
  Stage 4 lesson promised in §1: the clock decides *which week's* challenge counts as current. In a test
  we pin it; in production it's the real time.
- **Beat 4 is the snapshot callback.** We read the loop's `Name` live, but we deliberately do **not**
  read its targets — those come from `challenge.Snapshot…` in beats 6–7. Name is decoration; the numbers
  are the frozen goal. If this still feels slippery, that distinction *is* Stage 4's whole point, now
  paying off.
- **Beat 5 filters in memory on purpose.** We ask the database only for "this user's activities," then
  do the game filtering in C#. Two reasons, both in §2: the `QualifyingSportTypes.Includes` helper is
  domain code (not SQL), and doing the local-date check in C# behaves identically on Postgres and on the
  SQLite test database. The `Sum(...)` calls are plain LINQ-to-objects here, not SQL.
- **`FallsInWeek` (your 🧩).** The only computation in the file. The trick is already solved for you by
  Stage 3's storage choice: `StartDateLocal.DateTime` is the local wall clock, so taking its `DateOnly`
  gives the local calendar date. Inclusive on both ends because Sunday is part of the week.

**Questions you'll probably ask:**

- *"Why pull all the user's activities instead of filtering in the query?"* — For v1 a single user has a
  small number of activities (sync keeps a ~30-day window), so the cost is negligible, and in-memory
  filtering lets us reuse the domain helper and stay provider-independent. When the table grows, pushing
  the user+week filter into SQL is the clean optimisation — noted in §9.
- *"Why `FirstOrDefaultAsync` in beat 3, not `SingleOrDefaultAsync`?"* — The unique index `(UserId,
  WeekStart)` already guarantees at most one row for this week, so "first" and "single" can't differ
  here; `First` just reads as "give me the match if there is one."
- *"Could I have called `ChallengeCalculator` inside the DTO?"* — No — keep the DTO a dumb data carrier.
  The handler orchestrates; the DTO just holds the answer.

**Checkpoint:** `dotnet build` clean.

### Step 4 — Api: add the GET to `ChallengeController` 🧩

**The plan, in plain words:** expose the query over HTTP. **Where it's used:** the browser/Scalar (and
later the frontend) call `GET /api/challenge/current`; the controller turns that into a MediatR send and
returns the DTO as JSON. You're **adding a method** to the controller Stage 4 created — not a new file.

You've written `GetLoops` and the Stage 4 `Select`, so the shape is familiar. Add:

```csharp
/// <summary>Live progress for the user's active challenge this week, or 200 with an empty body when
/// none has been picked yet.</summary>
[HttpGet("current")]
[ProducesResponseType(typeof(CurrentChallengeDto), StatusCodes.Status200OK)]
public async Task<ActionResult<CurrentChallengeDto?>> Current(CancellationToken cancellationToken)
    => Ok(await sender.Send(new GetCurrentChallengeQuery(), cancellationToken));
```

Two notes:
- `Ok(null)` still returns **200** with an empty body — that's exactly the empty-state behaviour we chose.
- You'll need a `using LoopQuest.Application.Challenges.Queries.GetCurrentChallenge;` at the top.

**Checkpoint:** `dotnet build` clean; the endpoint shows up in Scalar.

### Step 5 — Tests: the handler proves the rules 📖 + 🧩

**The plan, in plain words:** prove the handler sums the *right* activities and matches the calculator.
**Where it's used:** these run in `dotnet test`; they're your safety net when Stage 6/7 start touching
challenges. Reuse Stage 3's `TestDb` (real `AppDbContext` on throwaway SQLite) and Stage 4's
`FixedTimeProvider` so time is pinned.

One full worked example (📖); the rest are yours (🧩):

```csharp
using LoopQuest.Application.Challenges.Queries.GetCurrentChallenge;
using LoopQuest.Domain.Entities;
using LoopQuest.Domain.Enums;
using LoopQuest.Domain.Time;
using LoopQuest.Infrastructure.Persistence;
using LoopQuest.Infrastructure.Tests.Sync;   // TestDb
using Microsoft.Data.Sqlite;

namespace LoopQuest.Infrastructure.Tests.Challenges;

public sealed class GetCurrentChallengeQueryHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SqliteConnection _connection;
    // Saturday 2026-06-13 10:00 UTC → week Mon 2026-06-08 .. Sun 2026-06-14 (Europe/Stockholm).
    private readonly FixedTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero));

    public GetCurrentChallengeQueryHandlerTests() => (_db, _connection) = TestDb.Create();
    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task<(User user, Loop loop)> SeedUserAndLoopAsync()
    {
        var user = User.Create(67890, "Viv M");   // default tz Europe/Stockholm
        var loop = Loop.Create("Alpine Test", "desc", LoopCategory.Classic, LoopTier.Hard, 50_000, 3_000);
        _db.Users.Add(user);
        _db.Loops.Add(loop);
        await _db.SaveChangesAsync();
        return (user, loop);
    }

    private async Task AddActiveChallengeAsync(User user, Loop loop)
    {
        var week = Week.Current(user.TimeZoneId, _clock.GetUtcNow());
        _db.WeeklyChallenges.Add(WeeklyChallenge.Create(
            user.Id, loop.Id, week, loop.TargetDistanceMeters, loop.TargetElevationMeters, _clock.GetUtcNow()));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Current_SumsOnlyQualifyingInWeekActivities_AndAppliesTheCalculator()
    {
        var (user, loop) = await SeedUserAndLoopAsync();
        await AddActiveChallengeAsync(user, loop);

        // two that COUNT (qualifying sport, inside the week)
        _db.Activities.Add(Activity.Create(user.Id, 1, "Run A", "Run",
            new DateTimeOffset(2026, 6, 9, 7, 0, 0, TimeSpan.Zero), 20_000, 400));
        _db.Activities.Add(Activity.Create(user.Id, 2, "Trail B", "TrailRun",
            new DateTimeOffset(2026, 6, 11, 7, 0, 0, TimeSpan.Zero), 15_000, 600));
        // one wrong SPORT (excluded), one OUTSIDE the week — neither should count
        _db.Activities.Add(Activity.Create(user.Id, 3, "Ride", "Ride",
            new DateTimeOffset(2026, 6, 10, 7, 0, 0, TimeSpan.Zero), 50_000, 1_000));
        _db.Activities.Add(Activity.Create(user.Id, 4, "Last week", "Run",
            new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero), 30_000, 800));
        await _db.SaveChangesAsync();

        var result = await new GetCurrentChallengeQueryHandler(_db, _clock)
            .Handle(new GetCurrentChallengeQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(35_000, result!.AchievedDistanceMeters);   // 20k + 15k only
        Assert.Equal(1_000, result.AchievedElevationMeters);    // 400 + 600 only
        Assert.Equal(50_000, result.TargetDistanceMeters);      // snapshot from the loop
        Assert.False(result.ChallengeComplete);                 // well short of 50k/3000
    }

    // 🧩 Current_NoActiveChallenge_ReturnsNull           (seed user+loop, NO challenge → result is null)
    // 🧩 Current_NoActivities_ReturnsZeroAchieved        (challenge but no runs → achieved 0, not null)
    // 🧩 Current_ExcludesActivitiesOutsideTheWeek        (one run on Sun 2026-06-14, one on Mon 2026-06-15)
    // 🧩 Current_BothTargetsMet_ChallengeComplete        (enough distance+elevation → all three flags true)
}
```

**What the worked test is really checking:** the four seeded activities are a little trap — only two of
them should survive the filter. If your `FallsInWeek` or the sport filter is wrong, the sums change and
the asserts catch it. The `35_000` / `1_000` numbers are deliberately the sum of *only* the two valid
runs.

For the 🧩 tests, the empty-state pair is the important one: `…ReturnsNull` (no challenge) vs
`…ReturnsZeroAchieved` (challenge exists, no runs) prove you've kept those two cases distinct — the same
"bug vs normal Tuesday" distinction from the handler.

**Checkpoint:** `dotnet test` — everything green.

### Step 6 — (Optional, dev only) See the bars fill without waiting for real runs 📖

**The plan, in plain words:** to *watch* progress in Scalar, you need activities dated **inside this
week**. Your real Strava runs might be from last week, so here's a dev-only way to drop a couple of fake
ones in. **This is for local play only — never a production path.**

The zero-code way (matches Stage 4's psql spot-check habit). First get your user id and confirm this
week's challenge exists, then insert two activities dated in the current week:

```bash
# 1. find your user id
docker exec -it <postgres-container> psql -U postgres -d loopquestdb \
  -c "select \"Id\" from users limit 1;"

# 2. insert two fake runs in THIS week (replace <USER_ID> and pick dates in the current Mon..Sun)
docker exec -it <postgres-container> psql -U postgres -d loopquestdb -c "
insert into activities
  (\"Id\",\"UserId\",\"StravaActivityId\",\"Name\",\"SportType\",\"StartDateLocal\",\"DistanceMeters\",\"ElevationGainMeters\",\"IngestedAt\")
values
  (gen_random_uuid(), '<USER_ID>', 900001, 'Dev Run 1', 'Run',      now(), 22000, 500, now()),
  (gen_random_uuid(), '<USER_ID>', 900002, 'Dev Trail', 'TrailRun', now(), 18000, 900, now());"
```

Then re-call `GET /api/challenge/current` and watch `AchievedDistanceMeters` / `AchievedElevationMeters`
jump. To reset, delete the rows by their `StravaActivityId` (900001/900002).

> The high `StravaActivityId` values (900001+) avoid colliding with real Strava ids, so a later real
> sync won't trip the unique index. Delete them before you rely on real data.

If you'd rather click than type SQL, the alternative is a tiny **dev-only** controller action guarded by
`IHostEnvironment.IsDevelopment()` — say the word in chat and we'll add it together (🤝). The psql route
is faster and adds no code, so it's the recommendation.

### Step 7 — The moment of truth 🤝

1. AppHost running → open Scalar (`/scalar/v1` on the api resource).
2. `GET /api/challenge/current` **before** picking anything → expect **200 with an empty body** (null).
3. `GET /api/loops`, copy an id, `POST /api/challenge/select { "loopId": "<id>" }` (Stage 4) → an Active
   challenge for this week.
4. `GET /api/challenge/current` again → now you get the DTO, with `achieved…` at **0** (no runs yet) and
   targets equal to the loop you picked.
5. Run Step 6's optional seeder (or `POST /api/sync` if you actually ran this week) → call current once
   more and watch the achieved numbers and completion flags update.

---

## 6. Cheat sheet

**Activity → week matching** (the one bit of logic):

```
local date of an activity = DateOnly.FromDateTime(activity.StartDateLocal.DateTime)
counts toward the week    = weekStart <= local date <= weekEnd   (inclusive both ends)
```

| | |
|---|---|
| Why no timezone maths here | `StartDateLocal` already holds the **local wall-clock** time — Strava converted it, Stage 3 stored it as-is. `.DateTime` reads that wall clock; `.UtcDateTime` would (wrongly) convert it. |
| Which challenge is "current" | this week's `Active` one — found via `Week.Current(tz, clock.GetUtcNow())` then matched on `WeekStart` |
| Targets come from | the **challenge snapshot** (`SnapshotTarget…`), never the live loop |
| Loop name comes from | the **live loop** (display only) |
| Empty state | `null` DTO → controller returns **200** with empty body |
| Calculator contract | `Calculate(ChallengeProgressInput(targetDist, targetElev, achievedDist, achievedElev))` → `ChallengeProgress(SurplusDistanceMeters, ElevationPurchasedMeters, EffectiveElevationMeters, DistanceComplete, ElevationComplete, ChallengeComplete)` |

---

## 7. Pitfalls (each is a real bug waiting)

> As before: none of these are compile errors. They build clean and lie at runtime.

1. **Reading targets off the live loop instead of the snapshot.** Use `challenge.SnapshotTarget…` in the
   calculator and the DTO. If you read `loop.TargetDistanceMeters`, a future loop edit silently rewrites
   an in-progress goal — the exact thing Stage 4's snapshot exists to prevent.
2. **Using `.UtcDateTime` (or converting) when matching the week.** `StartDateLocal` is *already* local.
   Convert it and a late-evening run can jump a day and fall out of (or into) the wrong week.
3. **Treating "no challenge" as an error.** Beat 3 returns `null`; it does not throw. Only "no user at
   all" throws. Mixing these up turns a normal start-of-week request into a 500.
4. **Off-by-one on the week bounds.** The range is **inclusive** on both ends — a Sunday run counts, a
   run on the *next* Monday does not. Your `…ExcludesActivitiesOutsideTheWeek` test pins this.
5. **Forgetting `AsNoTracking()`.** Not a correctness bug, but it's a read — tag it so EF doesn't keep
   change-tracking state it'll never use.
6. **Counting non-qualifying sports.** `Ride`, `Swim`, `VirtualRun`, etc. must not count. The
   `QualifyingSportTypes.Includes` filter is what keeps them out; don't drop it when summing.
7. **Summing the wrong field.** Distance sums `DistanceMeters`; elevation sums `ElevationGainMeters`.
   Easy to cross when copy-pasting the two `Sum` lines.

---

## 8. Definition of Done

- [ ] `dotnet build` — no new warnings/errors
- [ ] `dotnet test` — green: handler test (sums only qualifying, in-week activities and matches the
      calculator) plus your 🧩 tests (null when no challenge; zero achieved when no runs; out-of-week
      excluded; both-targets-met → complete)
- [ ] `GET /api/challenge/current` returns **200 with an empty body** when no challenge is active
- [ ] After selecting a loop, the endpoint returns the DTO with the **snapshot** targets and **0**
      achieved; after activities exist in the week, the achieved/effective/purchased numbers and the
      completion flags are correct
- [ ] No migration was needed; no new package references; dependency rule intact
- [ ] New code carries light comments (`///` on the public types, a `//` only where it isn't obvious)
- [ ] Reviewed, then merged: `git switch dev && git merge --no-ff feature/stage-5-live-progress`

## 9. Explicitly out of scope (resist the urge)

- **Completion/failure resolution + `CompletedAt`** — Stage 7's weekly rollover. Stage 5 only *reports*
  whether both targets are currently met; it never flips `Status` or stamps `CompletedAt`.
- **Pushing the activity filter into SQL** — fine as a later optimisation once the table is large; v1
  filters in memory for clarity and provider-independence.
- **Calibrated Safe/Step/Reach options** — Stage 6.
- **A public progress view** — Stage 8 (and it must expose only derived game state, never raw activities).
- **A history / past-weeks endpoint** — Stages 7/8.
- **Showing more than one active challenge** — there's at most one per week by the Stage 4 invariant; the
  endpoint reports exactly this week's.
```