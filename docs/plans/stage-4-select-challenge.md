# Stage 4 Plan — Select a Weekly Challenge

> **How to use this doc:** work top to bottom. Every task is tagged:
> **🧩 you write it** · **📖 reference example — type it, adapt it, understand it** · **🤝 we do it together in chat**.
> Drafted by Claude (2026-06-13), in the shape that made the Stage 3 plan work for us.
> This plan assumes Stage 3 was built per its plan (`Activity`, the SQLite `TestDb` handler-test
> setup, the unique-index-as-backstop habit) — if Stage 3 drifted, we update this doc first.
>
> **Same two words as before:** a *Stage* is a roadmap chapter (this doc is Stage 4); a *Step* is a
> numbered task inside it. Say which one you mean in chat and nobody gets lost.
>
> **When a step feels foggy**, the rescue order is: ① that step's *"what you're actually doing"* list
> ② its *"questions you'll probably ask"* ③ the domain model's [The week](../02-domain-model.md#the-week)
> and [WeeklyChallenge](../02-domain-model.md#entities) sections (the authoritative rules)
> ④ ask Claude *"what do I actually type?"* — a normal question, not a failure. Keep writing
> plain-language comments as you go: `///` docs on public types, `//` notes in the tricky spots.
>
> **Dependency note:** the only *hard* dependency is the loop library (Stage 0 ✅) — selecting a
> challenge doesn't need activities. But **start after Stage 3 is merged to `dev`**, because the
> handler tests reuse Stage 3's `TestDb`/SQLite setup. Then:
> `git switch dev && git switch -c feature/stage-4-select-challenge`

---

## 1. What you're building

`POST /api/challenge/select` turns one loop from the library into **your active challenge for this
week**. It's the first time the app makes a *game decision* and freezes a goal.

```
POST /api/challenge/select   { "loopId": "…" }
   │
   ▼
SelectChallengeCommand        ← validator: loopId not empty (a 400 if it is)
   │
   ▼
SelectChallengeCommandHandler (Application)
   │ 1. load the single user            (we need its TimeZoneId + Id)
   │ 2. load the loop by id             (must exist & be active) ─── the snapshot source
   │ 3. week = Week.Current(user.TimeZoneId, clock.GetUtcNow())   ← Mon..Sun, in LOCAL time
   │ 4. already an Active challenge this week? ──▶ yes: reject (one challenge per week)
   │ 5. WeeklyChallenge.Create(userId, loopId, week, loop.TargetDistance, loop.TargetElevation, now)
   │ 6. add + SaveChanges
   ▼
WeeklyChallengeDto { loopName, weekStart, weekEnd, status, snapshot targets }
```

**Why this stage matters:** Stage 3 stored *raw runs*. Stage 4 creates the *game object* — the
`WeeklyChallenge` — that Stage 5 will fill with live progress and Stage 7 will resolve to
Completed/Failed. The signature new idea is the **snapshot**: the challenge copies the loop's two
target numbers at selection time, so editing the library next month can never rewrite a goal you
already committed to. The numbered lines above are the same numbered beats as the handler recipe in
Step 2 — one picture, one recipe, same numbers.

### The three complexities of this stage, in plain words

1. **Week math.** A week is Monday 00:00 → Sunday 23:59:59, computed in the *user's* time zone.
   It's just arithmetic, but two traps live here: .NET counts days from Sunday, and "now" must be
   converted to local time before you take the date. Bugs hide on the Sunday→Monday seam.
2. **Snapshot.** The challenge stores its *own copy* of the loop's targets — numbers, not a live
   link. Copy them at `Create` time and the goal is frozen forever; look them up from the loop later
   and a library edit silently rewrites history.
3. **One challenge per week.** A friendly check ("you already picked this week") gives the message; a
   **unique index** on `(UserId, WeekStart)` is the database-level guarantee behind it — the exact
   same backstop pattern as Stage 3's unique `StravaActivityId`.

…plus one **new tool** that makes complexity #1 testable:

4. **An injected clock (`TimeProvider`).** So a test can say "pretend it's Sunday 23:59" and check
   which week you computed. Production gets the real clock; tests get a fixed one. This is the
   grown-up version of Stage 3's "pass `now` in as a parameter" lesson.

### New words in this stage

| Word | What it actually is | You'll meet it in |
|---|---|---|
| Snapshot | copying the loop's two target numbers onto the challenge at selection, so later library edits never touch it | Step 1 entity · Step 2 beat 5 |
| Monday-anchored week | weeks start Monday; but `DayOfWeek` calls Sunday `0` and Monday `1`, so the conversion needs care | Step 1 `Week.ContainingDate` |
| IANA time zone | the `"Europe/Stockholm"` id format. Modern .NET (6+) resolves these on Windows too, via ICU | Step 1 `Week.Current` |
| `TimeProvider` | .NET's built-in, swappable clock. `GetUtcNow()` in prod is the real time; a fake one in tests is whatever you set | Step 2 handler · Step 4 tests |
| `DateOnly` | a calendar date with no time-of-day — the right type for week boundaries. Npgsql stores it as a `date` column | the entity & config |
| Value object | a small type defined entirely by its values. Here `Week` is built only through factories, so it can never be malformed | Step 1 `Week` |
| Unique index (backstop) | the DB-level guarantee sitting behind the handler's friendly check — same idea as Stage 3's `StravaActivityId` | Step 3 config |
| Conflict (409) | the HTTP status that *fits* "already selected". v1 throws a clear `500` instead and we map it properly later | Step 2 · §9 |

---

## 2. Decisions already made (challenge them if they seem wrong)

| Decision | Choice | Why |
|---|---|---|
| One challenge per week | Reject a second selection while one is `Active`; **unique index `(UserId, WeekStart)`** as the backstop | Matches the roadmap's DoD. Simplest correct invariant: a week has exactly one challenge, which later transitions Active→Completed/Failed. "Change your pick mid-week" is a deliberate non-feature (§9). |
| Snapshot the targets | Copy `loop.TargetDistanceMeters` / `…Elevation` onto the challenge at `Create` | Domain rule: "Editing the loop library later must never retroactively change a past or active challenge." The snapshot columns *are* that guarantee. |
| Week shape | Monday 00:00 → Sunday 23:59:59, ISO-style | Domain model, [The week](../02-domain-model.md#the-week). |
| Where the week is computed | In the user's **IANA time zone**, from an **injected `TimeProvider`** | Bucketing is local-time by rule; an injected clock makes the boundary cases unit-testable instead of "works unless you run it at midnight". |
| `WeekStart` / `WeekEnd` type | `DateOnly` | They're calendar dates, not instants. Npgsql maps `DateOnly` → `date`. |
| Status storage | `ChallengeStatus` enum, persisted **as a string** (`HasConversion<string>()`) | House rule. Ints would corrupt meaning if the enum is ever reordered. |
| Validation | A FluentValidation validator on `LoopId` (`NotEmpty`); existence/conflict checks live in the **handler** | Input-*shape* checks belong in the validator; checks that need the database belong in the handler. Mirrors Stage 2's split. |
| Error mapping | `InvalidOperationException` with a clear message for "loop not found" / "already selected" → `500` in v1 | The same deliberate rough edge Stage 3 accepted. Mapping these to `404`/`409` is a clean later polish (§9), or a small exercise. |
| Handler tests | SQLite in-memory + Stage 3's `TestDb`, plus a hand-rolled `FixedTimeProvider` | Reuse what Stage 3 built; real EF SQL, no Docker. |
| New packages | **None.** `TimeProvider` is in the BCL; the fake clock is hand-rolled | Keeps Application package-free, exactly like Stage 3. |

---

## 3. One-time setup

**Nothing to install.** `TimeProvider` ships in the framework, `DateOnly` is built in, and the SQLite
test setup already exists from Stage 3. The only wiring is one line of DI in Step 2.

---

## 4. The map — every new file

```
src/LoopQuest.Domain/
  Enums/ChallengeStatus.cs                                       📖  (Active / Completed / Failed)
  Time/Week.cs                                                   📖 + 🧩  (ContainingDate given; Current is yours)
  Entities/WeeklyChallenge.cs                                    🧩  (mirror Activity.cs/Loop.cs; Create + snapshot)
src/LoopQuest.Application/
  Common/Interfaces/IAppDbContext.cs                             🧩  add DbSet<WeeklyChallenge>
  DependencyInjection.cs                                         🧩  register TimeProvider.System (one line)
  Challenges/Commands/SelectChallenge/SelectChallengeCommand.cs          🧩
  Challenges/Commands/SelectChallenge/SelectChallengeCommandValidator.cs 🧩  (your first validator since Stage 2)
  Challenges/Commands/SelectChallenge/SelectChallengeCommandHandler.cs   🧩  (the centrepiece)
  Challenges/Commands/SelectChallenge/WeeklyChallengeDto.cs              🧩
src/LoopQuest.Infrastructure/
  Persistence/Configurations/WeeklyChallengeConfiguration.cs     🧩  (two FKs, unique index, enum-as-string)
  Persistence/AppDbContext.cs                                    🧩  add DbSet<WeeklyChallenge>
  Persistence/Migrations/…AddWeeklyChallenges…                   🤝  (generated; we review together)
tests/LoopQuest.Domain.Tests/
  WeekTests.cs                                                   🧩
  WeeklyChallengeTests.cs                                        🧩
tests/LoopQuest.Infrastructure.Tests/
  Challenges/FixedTimeProvider.cs                                📖
  Challenges/SelectChallengeCommandHandlerTests.cs               📖 + 🧩  (reuses Stage 3's TestDb)
src/LoopQuest.Api/
  Controllers/ChallengeController.cs                             🧩  (POST api/challenge/select)
```

---

## 5. Build order

### Step 1 — Domain: the week, the status, the challenge 🧩 📖

**What you're actually doing:** ① a tiny `ChallengeStatus` enum ② the `Week` value object (the math
trap) ③ the `WeeklyChallenge` entity ④ tests for the week math and the entity. All Domain — no clock,
no DB, no DI.

**(a) `ChallengeStatus`** (📖 — three lines):

```csharp
namespace LoopQuest.Domain.Enums;

public enum ChallengeStatus { Active, Completed, Failed }
```

**(b) `Week`** — the signature tricky bit of this stage. `ContainingDate` is handed to you (📖,
because the off-by-one is a rite of passage you only need to survive once); `Current` is yours (🧩):

```csharp
namespace LoopQuest.Domain.Time;

/// <summary>A Monday→Sunday calendar week. Built only through these factories, so a Week is always
/// exactly seven days, Monday to Sunday — it can never be malformed.</summary>
public readonly record struct Week(DateOnly Start, DateOnly End)
{
    /// <summary>The week that contains <paramref name="date"/>. Pure: no clock, no time zone.</summary>
    public static Week ContainingDate(DateOnly date)
    {
        // .NET's DayOfWeek counts Sunday as 0, Monday as 1 … Saturday as 6. We want "days since
        // Monday" (Mon=0 … Sun=6), so shift by 6 and wrap with % 7. Get this wrong and every Sunday
        // falls into the next week — the exact off-by-one this helper exists to get right, once.
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var start = date.AddDays(-daysSinceMonday);
        return new Week(start, start.AddDays(6));
    }

    // 🧩 public static Week Current(string ianaTimeZoneId, DateTimeOffset utcNow)
    //    ① var tz    = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
    //    ② var local = TimeZoneInfo.ConvertTime(utcNow, tz);
    //    ③ return ContainingDate(DateOnly.FromDateTime(local.DateTime));
    // Note it takes utcNow as a PARAMETER (like Stage 1's IsExpiredOrExpiringWithin took `now`) —
    // that's what lets a test pin the clock to a boundary.
}
```

**(c) `WeeklyChallenge`** (🧩) — the entity pattern you've now built three times (`Loop`, `User`,
`Activity`): private parameterless ctor for EF, private setters, a static `Create(...)` with guards.

```csharp
public static WeeklyChallenge Create(
    Guid userId,
    Guid loopId,
    Week week,
    double snapshotTargetDistanceMeters,
    double snapshotTargetElevationMeters,
    DateTimeOffset selectedAt)
```

Where each property comes from — keep this beside you while writing `Create`:

| `WeeklyChallenge` property | Filled from | Notes |
|---|---|---|
| `Id` | `Guid.NewGuid()` inside `Create` | our key |
| `UserId`, `LoopId` | the params | FKs in Step 3 |
| `WeekStart`, `WeekEnd` | `week.Start`, `week.End` | the `Week` value already guarantees Mon..Sun |
| `Status` | `ChallengeStatus.Active` | a freshly selected challenge is always Active |
| `SnapshotTargetDistanceMeters`, `…Elevation` | the params | **the frozen copy** — see pitfall #3 |
| `SelectedAt` | the `selectedAt` param | passed in, not `DateTimeOffset.UtcNow` — same testability reason as `Week.Current` |
| `CompletedAt` | `null` | set at rollover, Stage 7 |
| `AchievedDistanceMeters`, `AchievedElevationMeters`, `ElevationPurchasedMeters` | `0` | computed in Stage 5; today they start empty |

Guards: `userId != Guid.Empty`, `loopId != Guid.Empty`, `snapshotTargetDistanceMeters > 0`,
`snapshotTargetElevationMeters >= 0`.

**Questions you'll probably ask in this step:**

- *"Why is `Week` a `record struct`, not a class?"* — it's two dates with no identity; two weeks with
  the same Start and End are the same week. `record struct` gives value equality for free and stays
  cheap. It's the textbook shape of a value object.
- *"Why store `WeekStart`/`WeekEnd` AND `LoopId`?"* — different jobs. `LoopId` says *which* loop (for
  the name, for history); the snapshot numbers say *what the goal was*, frozen. You keep both.
- *"Why pass `selectedAt` in instead of reading the clock?"* — same answer as everywhere in this
  stage: a passed-in time can be pinned in a test; a buried `DateTimeOffset.UtcNow` cannot.

**Tests first** (🧩), suggested names:
- `WeekTests`: `ContainingDate_Monday_StartsThatDay` · `ContainingDate_Sunday_StartsSixDaysEarlier` ·
  `ContainingDate_WrapsAcrossAMonthBoundary` · `Current_UsesUserTimeZone_NotUtc` (the boundary one —
  see the cheat sheet for a concrete pair).
- `WeeklyChallengeTests`: `Create_SetsStatusActiveAndZeroAchieved` · `Create_SnapshotsTheTargets` ·
  `Create_RejectsEmptyUserId` · `Create_RejectsNonPositiveSnapshotDistance`.

**Checkpoint:** `dotnet test tests/LoopQuest.Domain.Tests` green.

### Step 2 — Application: the slice + the injected clock 🧩

A full vertical slice this time (the richest since Stage 2): a command **with input**, so it gets a
**validator**; a DTO; and the handler. The handler's ingredients are `IAppDbContext` and — new —
`TimeProvider`.

**Write order (so each compile error points at the next file):** ① `DbSet<WeeklyChallenge>` on
`IAppDbContext` ② the command ③ the validator ④ the DTO ⑤ the handler ⑥ the DI line.

`SelectChallengeCommand`:

```csharp
public sealed record SelectChallengeCommand(Guid LoopId) : IRequest<WeeklyChallengeDto>;
```

The validator (🧩 — your first since Stage 2; copy `CompleteStravaConnectionCommandValidator`'s shape):
`RuleFor(c => c.LoopId).NotEmpty();`

`WeeklyChallengeDto` (🧩) — the safe projection, never the entity:

```csharp
public sealed record WeeklyChallengeDto(
    Guid Id, Guid LoopId, string LoopName,
    DateOnly WeekStart, DateOnly WeekEnd, string Status,
    double SnapshotTargetDistanceMeters, double SnapshotTargetElevationMeters);
```

**The handler recipe** (🧩 — the centrepiece; constructor is `(IAppDbContext db, TimeProvider clock)`):

1. Load the user: `db.Users.SingleOrDefaultAsync(cancellationToken)` — no predicate, because v1 is
   single-user (returns the one user; `null` if none; throws if somehow two — a fine guard for now).
   `null`? Throw `InvalidOperationException("No user yet — connect Strava first.")`
2. Load the loop: `db.Loops.FirstOrDefaultAsync(l => l.Id == request.LoopId && l.IsActive, ct)`.
   `null`? Throw `InvalidOperationException("Loop not found or inactive.")`
3. `var week = Week.Current(user.TimeZoneId, clock.GetUtcNow());`
4. Already taken? `var exists = await db.WeeklyChallenges.AnyAsync(c => c.UserId == user.Id &&
   c.WeekStart == week.Start && c.Status == ChallengeStatus.Active, ct);` → `true`? Throw
   `InvalidOperationException($"You already have an active challenge for the week of {week.Start}.")`
5. `var challenge = WeeklyChallenge.Create(user.Id, loop.Id, week,
       loop.TargetDistanceMeters, loop.TargetElevationMeters, clock.GetUtcNow());`
6. `db.WeeklyChallenges.Add(challenge); await db.SaveChangesAsync(ct);`
7. Return a `WeeklyChallengeDto` projected from `challenge` + `loop.Name`.

Then the **one DI line** in `Application/DependencyInjection.cs`'s `AddApplication`:

```csharp
services.AddSingleton(TimeProvider.System);   // the real clock; tests substitute a fixed one
```

**Questions you'll probably ask in this step:**

- *"Beat 5 reads `loop.TargetDistanceMeters` and copies it. Isn't storing `LoopId` enough?"* — No, and
  that's the whole stage. `LoopId` is a pointer that follows the loop's *current* values; the snapshot
  is a photograph of the values *now*. Edit the loop next month and the pointer moves, the photograph
  doesn't. We want the photograph.
- *"Why `AnyAsync` and not `SingleOrDefaultAsync` in beat 4?"* — we don't need the existing row, only
  whether one exists. `AnyAsync` asks the database a yes/no question and is the cheapest way to do it.
- *"Why filter on `Status == Active` in beat 4 when the index covers any status?"* — the filter gives
  a precise message; the index guarantees correctness. In v1 they agree (a week only ever holds one
  challenge), so the filter is really about the *wording* of the error.

**Checkpoint:** `dotnet build` clean.

### Step 3 — Persistence: configuration + migration 🧩 🤝

`WeeklyChallengeConfiguration` is your fourth configuration — hints only:

- `ToTable("weekly_challenges")`, key on `Id`.
- `HasIndex(c => new { c.UserId, c.WeekStart }).IsUnique()` ← **the one-per-week backstop.**
- `Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired()` ← enum as string.
- **Two** foreign keys this time (Stage 3 introduced the first); no navigation properties, which is fine:

  ```csharp
  builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId);
  builder.HasOne<Loop>().WithMany().HasForeignKey(c => c.LoopId);
  ```

Add `DbSet<WeeklyChallenge> WeeklyChallenges => Set<WeeklyChallenge>();` to `AppDbContext`, then
generate the migration (**you run it** — our standing rule):

```powershell
dotnet ef migrations add AddWeeklyChallenges --project src/LoopQuest.Infrastructure --startup-project src/LoopQuest.Infrastructure --output-dir Persistence/Migrations
```

🤝 Paste the generated migration into chat — we check together that the unique index landed, both FKs
landed, `Status` is a `text`/`varchar` column (not an int), and `WeekStart`/`WeekEnd` are `date`
columns. (`DateOnly` → `date` is native in EF Core 10 / Npgsql; confirm it in the migration rather
than trusting this line.)

**Checkpoint:** migration exists, `dotnet build` clean.

### Step 4 — Tests: the handler proves the rules 📖 + 🧩

**(a)** The hand-rolled fixed clock (📖 — `TimeProvider` is abstract; override the one method you need):

```csharp
// Challenges/FixedTimeProvider.cs — "Strava-fake" energy, one layer down: the test owns time.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

**(b)** Handler tests — reuse Stage 3's `TestDb` (real `AppDbContext` on throwaway SQLite). One full
example (📖); the rest are yours (🧩):

```csharp
public sealed class SelectChallengeCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero));

    public SelectChallengeCommandHandlerTests() => (_db, _connection) = TestDb.Create();
    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task<(User user, Loop loop)> SeedUserAndLoopAsync()
    {
        var user = User.Create(67890, "Viv M");            // default tz Europe/Stockholm
        var loop = Loop.Create("Alpine Test", "desc", LoopCategory.Classic, LoopTier.Hard, 50_000, 3_000);
        _db.Users.Add(user);
        _db.Loops.Add(loop);
        await _db.SaveChangesAsync();
        return (user, loop);
    }

    [Fact]
    public async Task Select_CreatesActiveChallenge_WithSnapshottedTargets()
    {
        var (_, loop) = await SeedUserAndLoopAsync();

        var result = await new SelectChallengeCommandHandler(_db, _clock)
            .Handle(new SelectChallengeCommand(loop.Id), CancellationToken.None);

        var stored = Assert.Single(_db.WeeklyChallenges);
        Assert.Equal(ChallengeStatus.Active, stored.Status);
        Assert.Equal(50_000, stored.SnapshotTargetDistanceMeters);   // frozen from the loop
        Assert.Equal(3_000,  stored.SnapshotTargetElevationMeters);
        Assert.Equal(new DateOnly(2026, 6, 8), result.WeekStart);    // Monday of the 13th's week
    }

    // 🧩 Select_SecondSelectionSameWeek_Throws       (select twice; second throws; still ONE row)
    // 🧩 Select_UnknownLoop_Throws                   (random Guid; InvalidOperationException; no row)
    // 🧩 Select_InactiveLoop_Throws                  (seed a loop with isActive:false)
    // 🧩 Select_WeekStart_IsTheUsersLocalMonday      (assert WeekStart from the pinned clock above)
}
```

A note on the "snapshot is frozen" guarantee: because `Create` copies the loop's numbers into the
challenge's own columns, no later loop edit can reach them — the freezing is *structural*, proven by
`Select_CreatesActiveChallenge_WithSnapshottedTargets` above. (A literal "edit the loop afterwards"
test has to wait until a loop-edit feature exists; `Loop` has no mutator today.)

**Checkpoint:** `dotnet test` — everything green.

### Step 5 — Api: `ChallengeController` 🧩

`POST /api/challenge/select`, route `api/challenge`. Bind the command straight from the body and send
it — you've written `LoopsController` and `AuthController`, so the shape is familiar:

```csharp
[HttpPost("select")]
public async Task<ActionResult<WeeklyChallengeDto>> Select(
    [FromBody] SelectChallengeCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));
```

Remember `[ProducesResponseType(typeof(WeeklyChallengeDto), StatusCodes.Status200OK)]` for the OpenAPI
doc. (`Ok` is the v1 choice; a stricter REST answer is `201 Created`, but there's no GET-by-id to
point a `Location` header at until Stage 5, so `200` is honest for now.)

**Checkpoint:** `dotnet build`, run the AppHost.

### Step 6 — The moment of truth 🤝

1. AppHost running → open Scalar (`/scalar/v1` on the api resource).
2. You need a loop id: execute `GET /api/loops` and copy one loop's `id`.
3. Execute `POST /api/challenge/select` with `{ "loopId": "<that id>" }`. You should get back an
   **Active** challenge whose `weekStart` is **this Monday** and whose snapshot targets equal that
   loop's targets.
4. **Select again** (same or different loop) → it must be **rejected**. One challenge per week, kept.
5. Spot-check the row:

   ```bash
   docker exec -it <postgres-container> psql -U postgres -d loopquestdb \
     -c "select \"LoopId\", \"WeekStart\", \"WeekEnd\", \"Status\", \"SnapshotTargetDistanceMeters\" from weekly_challenges"
   ```

   One row, `Status = Active`, `WeekStart` a Monday, `WeekEnd` the Sunday six days later.

---

## 6. Week-math cheat sheet

`System.DayOfWeek` numbering — the source of the off-by-one:

| Sun | Mon | Tue | Wed | Thu | Fri | Sat |
|---|---|---|---|---|---|---|
| 0 | 1 | 2 | 3 | 4 | 5 | 6 |

The move: `daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;` then `start = date.AddDays(-daysSinceMonday)`.

Worked example (uses *today*, 2026-06-13, a **Saturday**):

```
date          = 2026-06-13 (Sat, DayOfWeek = 6)
daysSinceMonday = (6 + 6) % 7 = 5
Start         = 2026-06-13 − 5 = 2026-06-08 (Mon)
End           = Start + 6      = 2026-06-14 (Sun)
```

The boundary pair your `Current_UsesUserTimeZone_NotUtc` test should pin (one tz where the seam bites):
a `utcNow` that is **Sunday 23:30 UTC** is, in `Europe/Stockholm` (UTC+2 in June), **Monday 01:30
local** — so the user is already in the *next* week even though it's still Sunday in UTC. That single
case proves both that you convert to local *and* that the Monday seam works.

| | |
|---|---|
| Week | Monday 00:00 → Sunday 23:59:59 |
| Bucket by | the user's **local** time (their `TimeZoneId`), never UTC |
| `DateOnly` → DB | `date` column (Npgsql native) — confirm in the migration |
| Enum → DB | `ChallengeStatus` as **string** (`HasConversion<string>()`) |

---

## 7. Pitfalls (each is a real bug waiting)

> As in Stage 3: none of these are compile errors. Every one **builds clean and lies at runtime.**
> Read this before Step 2, and again before Step 6.

1. **`DayOfWeek` starts on Sunday.** `(int)DayOfWeek.Sunday == 0`, `Monday == 1`. Compute "days since
   Monday" with the `+ 6) % 7` shift; a naive `- 1` puts Sundays in the wrong week and nothing tells you.
2. **Bucket by local time, not UTC.** Convert `utcNow` to the user's zone *before* taking the date
   (`Week.Current` does this). The boundary case in the cheat sheet is exactly the bug this prevents.
3. **Snapshot means copy, not look-up.** Store the loop's target *numbers* on the challenge. If you
   instead read them from the loop when displaying progress later, editing the loop silently rewrites a
   past goal. The snapshot columns exist *only* to be frozen.
4. **Enum as string.** `Status` needs `HasConversion<string>()` (house rule). Stored as an int, a
   future reordering of `ChallengeStatus` reinterprets every old row.
5. **The unique index is the backstop; the handler check is the message.** Keep both, same as Stage 3.
   The check gives a clear sentence; the index guarantees correctness even under a double-click race.
6. **Inject the clock — don't bury `DateTimeOffset.UtcNow`.** A buried clock makes the week
   un-pinnable in tests and can flake at the real Sunday→Monday midnight. Take the time from
   `TimeProvider`, pass it into `Week.Current` and into `Create`.
7. **`FindSystemTimeZoneById` throws on a bad id.** Our users default to `"Europe/Stockholm"` (valid),
   so v1 is fine — but know that an unknown id is an exception, not a silent default. Hardening that is
   a later concern, not today's.
8. **`DateOnly` vs `DateTime`.** Use `DateOnly` for `WeekStart`/`WeekEnd`. Don't reach for `DateTime`
   and a midnight time-of-day — it invites the timezone math we deliberately keep out of these fields.

---

## 8. Definition of Done

- [ ] `dotnet build` — no new warnings/errors
- [ ] `dotnet test` — green: `Week` boundary tests + `WeeklyChallenge` entity tests + handler tests
      (creates an Active challenge with snapshotted targets; second selection rejected; unknown/inactive loop rejected)
- [ ] `POST /api/challenge/select` creates an Active challenge for the current week with snapshotted targets
- [ ] **A second selection for the same week is rejected and the row count stays 1**
- [ ] Migration has the unique index on `(UserId, WeekStart)`, both FKs (users, loops), `Status` as a
      string column, and `WeekStart`/`WeekEnd` as `date` columns
- [ ] Application gained no new package references; dependency rule intact
- [ ] New code carries the plain-language comments future-you needs (`///` on public types, `//` in the tricky spots)
- [ ] Reviewed, then merged: `git switch dev && git merge --no-ff feature/stage-4-select-challenge`

## 9. Explicitly out of scope (resist the urge)

- **Computing progress / summing activities** — Stage 5. Today the achieved fields are `0` on purpose.
- **Completion / failure resolution and `CompletedAt`** — Stage 7's weekly rollover. `WeeklyChallenge`
  grows `Complete()` / `Fail()` mutators then, not now.
- **Calibrated Safe/Step/Reach options** — Stage 6. This stage selects a loop you name explicitly.
- **Changing your pick mid-week / re-selecting** — deliberately disallowed in v1 (one challenge per
  week). If we ever allow it, the unique index becomes a *filtered* index on `Status = Active`.
- **`404`/`409` problem-details mapping** — a later polish; v1 throws clear `InvalidOperationException`s.
- **A history or "current challenge" read endpoint** — Stages 5, 7, 8.
