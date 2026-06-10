# Stage 3 Plan — Sync Activities

> **How to use this doc:** work top to bottom. Every task is tagged:
> **🧩 you write it** · **📖 reference example — type it, adapt it, understand it** · **🤝 we do it together in chat**.
> Drafted by Claude (2026-06-10) for Viv's review. This plan assumes Stage 2 was built per its plan
> (`IStravaClient`, `StravaClient`, `User`/`StravaConnection`) — if Stage 2 drifted, we update this doc first.
>
> **Start only after Stage 2 is merged to `dev`.** Then: `git switch dev && git switch -c feature/stage-3-sync-activities`

---

## 1. What you're building

`POST /api/sync` pulls your recent Strava activities into the `activities` table. Run it twice —
nothing duplicates. That property has a name, **idempotency**, and it's the heart of this stage.

```
POST /api/sync
   │
   ▼
SyncActivitiesCommand handler (Application)
   │ 1. load the user (+ connection)
   │ 2. token about to expire? ──▶ RefreshAsync ──▶ save rotated tokens IMMEDIATELY
   │ 3. GetActivitiesAsync(accessToken, after = 30 days ago)
   │       └─ Infrastructure loops Strava's pages: 1, 2, 3… until a short page
   │ 4. keep only Run / TrailRun / Hike / Walk
   │ 5. upsert by StravaActivityId ──▶ seen before? Update : Add
   ▼
SyncResultDto { fetched, qualifying, added, updated }   ← makes idempotency *visible*
```

### The three complexities of this stage, in plain words

1. **Pagination.** Strava won't hand you everything at once — you ask for page 1, then page 2, …
   until a page comes back short. It's just a loop, but the stop condition is where bugs live.
2. **Token refresh, for real this time.** Stage 2 built `RefreshAsync`; now something *uses* it.
   Before calling Strava, the handler checks "does my token expire soon?" and refreshes if so.
   The trap: Strava *rotates* refresh tokens, so the new ones must hit the database **before**
   anything else can fail — or you lose the ability to refresh ever again.
3. **Upsert (= update-or-insert).** Strava is the source of truth; our table is a copy. Re-syncing
   must update what changed (renamed run, corrected elevation) and add what's new — never duplicate.
   The unique index on `StravaActivityId` is the database-level backstop if our logic slips.

### New concepts in this stage

| Concept | One-liner |
|---|---|
| Pagination loop | Fetch page after page until a short page says "that's all". |
| Per-request bearer auth | Build an `HttpRequestMessage`, set its `Authorization` header — the token varies per call, so it can't live in DI config. |
| Idempotency | Running the same command twice leaves the same end state. |
| Hand-rolled fakes | A test class implementing `IStravaClient` yourself — no mocking library needed. |
| SQLite in-memory DB | Run real EF queries against a throwaway database living in RAM — perfect for handler tests. |

---

## 2. Decisions already made (challenge them if they seem wrong)

| Decision | Choice | Why |
|---|---|---|
| What to store | **Only qualifying** sport types (Run, TrailRun, Hike, Walk) | Roadmap says filter at sync. Smaller table; Stage 5 needn't re-filter. Trade-off: if you later include `VirtualRun`, only *future* syncs pick it up (a re-sync backfills 30 days — acceptable). |
| Where the allowlist lives | `Domain/Activities/QualifyingSportTypes.cs` | It's a game rule. The "one-line config change" from doc 02 is literally one line here. |
| Sync window | Fixed lookback: `now − 30 days`, every sync | No "last sync" bookkeeping, no new state, idempotent by construction — and it covers *last* week, which Stage 6's calibration will need. Revisit in Stage 9 if it ever feels slow (it won't: ~1 request per sync for one athlete). |
| How the token reaches Strava calls | Passed as a plain `string accessToken` parameter | Explicit beats magic for v1. (The "invisible" alternative — a DelegatingHandler that injects/refreshes tokens — is a lovely refactor for later, not a first version.) |
| Who knows about pages | Only `StravaClient` | The handler asks for "activities after X" and gets a complete list. Paging is a wire detail — Infrastructure's job. |
| Where refresh is orchestrated | Explicitly in the handler, persisted immediately | You can read the whole story top to bottom in one method. Expiry check itself (`IsExpiredOrExpiringWithin`) is pure domain logic → unit-testable. |
| Handler tests | In `tests/LoopQuest.Infrastructure.Tests`, real `AppDbContext` on **SQLite in-memory** + a hand-rolled `FakeStravaClient` | Tests the real EF queries without Docker. No new test project: Infrastructure.Tests already references everything needed. |
| New package | `Microsoft.EntityFrameworkCore.Sqlite` **10.0.8** (test project only) | Matches the pinned EF line. |

---

## 3. One-time setup

1. Add to `Directory.Packages.props`:

   ```xml
   <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.8" />
   ```

2. Add to `tests/LoopQuest.Infrastructure.Tests/LoopQuest.Infrastructure.Tests.csproj`:

   ```xml
   <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
   ```

That's it — Strava app and secrets already exist from Stage 2.

---

## 4. The map — every new file

```
src/LoopQuest.Domain/
  Entities/Activity.cs                                  🧩  (mirror Loop.cs/User.cs)
  Activities/QualifyingSportTypes.cs                    📖
  ValueObjects/StravaConnection.cs                      🧩  add IsExpiredOrExpiringWithin(...)
  Entities/User.cs                                      🧩  add RefreshTokens(...) — keeps Scope
src/LoopQuest.Application/
  Common/Interfaces/IStravaClient.cs                    📖  add GetActivitiesAsync + record
  Common/Interfaces/IAppDbContext.cs                    🧩  add DbSet<Activity>
  Activities/Commands/SyncActivities/SyncActivitiesCommand.cs        🧩
  Activities/Commands/SyncActivities/SyncActivitiesCommandHandler.cs 🧩  (the centrepiece)
  Activities/Commands/SyncActivities/SyncResultDto.cs                🧩
src/LoopQuest.Infrastructure/
  Strava/StravaClient.cs                                📖  add GetActivitiesAsync + wire DTO
  Persistence/Configurations/ActivityConfiguration.cs   🧩  (your third one — hints only)
  Persistence/AppDbContext.cs                           🧩  add DbSet<Activity>
  Persistence/Migrations/…AddActivities…                🤝  (generated; we review together)
tests/LoopQuest.Domain.Tests/
  ActivityTests.cs, StravaConnectionTests.cs, UserTests.cs (additions)   🧩
tests/LoopQuest.Infrastructure.Tests/
  Strava/FakeHttpMessageHandler.cs                      📖  small upgrade: a queue of responses
  Strava/StravaClientTests.cs (additions)               🧩
  Sync/FakeStravaClient.cs                              📖
  Sync/TestDb.cs                                        📖
  Sync/SyncActivitiesCommandHandlerTests.cs             📖 + 🧩
src/LoopQuest.Api/
  Controllers/SyncController.cs                         🧩  (you've seen two controllers — no snippet)
```

No validator this time: `SyncActivitiesCommand` has no inputs, and parameterless requests pass
straight through the validation pipeline.

---

## 5. Build order

### Step 1 — Domain: `Activity` + three small additions 🧩

**`Activity`** mirrors the entity pattern you now know: private parameterless ctor, private setters,
`Create(...)` with guards (`stravaActivityId > 0`, `userId` not empty, distance/elevation ≥ 0,
name trimmed). Fields per [02-domain-model.md](../02-domain-model.md): `Id`, `UserId`,
`StravaActivityId`, `Name`, `SportType`, `StartDateLocal` (DateTimeOffset), `DistanceMeters`,
`ElevationGainMeters`, `IngestedAt`. One *new* wrinkle — a mutator for re-sync:

```csharp
public void UpdateFromSync(string name, string sportType, DateTimeOffset startDateLocal,
    double distanceMeters, double elevationGainMeters)
{ /* 🧩 same guards as Create — consider a shared private Validate(...) so rules live once */ }
```

**The allowlist** (📖 — it pins a game rule exactly):

```csharp
// Domain/Activities/QualifyingSportTypes.cs
public static class QualifyingSportTypes
{
    // VirtualRun (treadmill) is deliberately excluded — adding it here is the "one-line change".
    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { "Run", "TrailRun", "Hike", "Walk" };

    public static bool Includes(string sportType) => All.Contains(sportType);
}
```

**On `StravaConnection`** (🧩): `bool IsExpiredOrExpiringWithin(TimeSpan buffer, DateTimeOffset now)`.
Note it *takes `now` as a parameter* instead of reading the clock itself — that's what makes the
boundary cases unit-testable. Never bury `DateTimeOffset.UtcNow` deep inside domain logic.

**On `User`** (🧩): `RefreshTokens(accessToken, refreshToken, expiresAt)` — like `ConnectStrava`
but it must **keep the existing `Scope`** (refresh responses don't include one).

**Tests first**, suggested names:
`Create_RejectsNonPositiveStravaActivityId` · `Create_RejectsNegativeDistance` ·
`UpdateFromSync_ReplacesTheSyncedFields` · `IsExpiredOrExpiringWithin_TrueWhenInsideBuffer` ·
`IsExpiredOrExpiringWithin_FalseWhenComfortablyValid` · `RefreshTokens_KeepsTheExistingScope`.

**Checkpoint:** `dotnet test tests/LoopQuest.Domain.Tests` green.

### Step 2 — Application: the contract grows, and the centrepiece handler

Addition to `IStravaClient` (📖 — anchors everything):

```csharp
/// <summary>All activities starting after <paramref name="after"/>, newest pages fetched until
/// exhausted. Paging is handled inside the implementation.</summary>
Task<IReadOnlyList<StravaActivitySummary>> GetActivitiesAsync(
    string accessToken, DateTimeOffset after, CancellationToken cancellationToken);

public sealed record StravaActivitySummary(
    long Id, string Name, string SportType, DateTimeOffset StartDateLocal,
    double DistanceMeters, double ElevationGainMeters);
```

Add `DbSet<Activity> Activities { get; }` to `IAppDbContext`.

**`SyncActivitiesCommand : IRequest<SyncResultDto>`** with
`record SyncResultDto(int Fetched, int Qualifying, int Added, int Updated);`

**The handler recipe** (🧩 — this is the most important code of the stage; take it slowly):

1. Load the user: `db.Users.SingleOrDefaultAsync(...)`. No user or no `Connection`? Throw
   `InvalidOperationException("No Strava connection — visit /auth/strava/connect first.")`
   (a 500 with a clear message is an acceptable v1 rough edge; we can map it to a 409 later).
2. If `user.Connection.IsExpiredOrExpiringWithin(TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow)`:
   call `RefreshAsync`, then `user.RefreshTokens(...)`, then **`SaveChangesAsync` immediately** —
   *before* the activities call. If anything fails later, the rotated token is already safe.
3. `var after = DateTimeOffset.UtcNow - TimeSpan.FromDays(30);` (a named constant).
4. `var fetched = await stravaClient.GetActivitiesAsync(user.Connection.AccessToken, after, ct);`
   ⚠️ read `user.Connection` *again* here, not a variable captured before step 2 — refreshing
   **replaced** the Connection object, so an old local would hold the dead token.
5. Filter: `fetched.Where(a => QualifyingSportTypes.Includes(a.SportType))`.
6. Upsert — the dictionary pattern (📖, the one genuinely new EF move):

   ```csharp
   var stravaIds = qualifying.Select(a => a.Id).ToList();
   var existing = await db.Activities
       .Where(a => stravaIds.Contains(a.StravaActivityId))
       .ToDictionaryAsync(a => a.StravaActivityId, cancellationToken);

   foreach (var item in qualifying)
   {
       if (existing.TryGetValue(item.Id, out var activity))
       {
           activity.UpdateFromSync(/* 🧩 */);     // EF change tracking writes only what changed
       }
       else
       {
           db.Activities.Add(Activity.Create(/* 🧩 */));
       }
   }
   ```

   One round-trip to load all matches (`Contains` becomes SQL `IN`), then a fast in-memory
   dictionary lookup per activity — instead of one query *per activity* (the classic "N+1" mistake).
7. `SaveChangesAsync`, return the counts (`Updated` = matches found; precise enough for v1).

**Checkpoint:** `dotnet build` clean.

### Step 3 — Infrastructure: the paged, authenticated call 📖

New method in `StravaClient` — this is your reference example for the stage. Two new tricks:
a per-request `Authorization` header (so we build `HttpRequestMessage` ourselves instead of
`GetAsync`), and the paging loop:

```csharp
private const int PageSize = 200;   // Strava's maximum per_page

public async Task<IReadOnlyList<StravaActivitySummary>> GetActivitiesAsync(
    string accessToken, DateTimeOffset after, CancellationToken cancellationToken)
{
    var results = new List<StravaActivitySummary>();

    for (var page = 1; ; page++)
    {
        var url = $"/api/v3/athlete/activities" +
                  $"?after={after.ToUnixTimeSeconds()}&page={page}&per_page={PageSize}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var batch = await response.Content
            .ReadFromJsonAsync<List<ActivityResponse>>(cancellationToken) ?? [];

        results.AddRange(batch.Select(a => new StravaActivitySummary(
            a.Id, a.Name, a.SportType, a.StartDateLocal, a.Distance, a.TotalElevationGain)));

        if (batch.Count < PageSize)
        {
            break;   // a short (or empty) page means we've reached the end
        }
    }

    return results;
}

// Add beside TokenResponse — Strava's wire shape, private to Infrastructure:
private sealed record ActivityResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sport_type")] string SportType,
    [property: JsonPropertyName("start_date_local")] DateTimeOffset StartDateLocal,
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("total_elevation_gain")] double TotalElevationGain);
```

Stare at the stop condition until you believe it: a full page (= exactly 200) *might* mean more
pages, so we loop again; a short page can't. Worst case we make one extra request when the total is
an exact multiple of 200 — correct beats clever.

**Checkpoint:** `dotnet build` clean.

### Step 4 — Persistence: configuration + migration 🧩 🤝

`ActivityConfiguration` is your third configuration — hints only this time:

- `ToTable("activities")`, key on `Id`.
- `HasIndex(a => a.StravaActivityId).IsUnique()` ← **the idempotency backstop.**
- `Name` max 300 required; `SportType` max 50 required.
- First foreign key in the project (📖, one line — there's no navigation property, and that's fine):

  ```csharp
  builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId);
  ```

Add `DbSet<Activity> Activities => Set<Activity>();` to `AppDbContext`, then:

```bash
dotnet ef migrations add AddActivities \
  --project src/LoopQuest.Infrastructure \
  --startup-project src/LoopQuest.Infrastructure \
  --output-dir Persistence/Migrations
```

🤝 Paste the generated migration into chat — we'll check the unique index and the FK landed right.

**Checkpoint:** migration exists, `dotnet build` clean.

### Step 5 — Tests: the handler proves its own idempotency 📖 + 🧩

**(a)** Upgrade `FakeHttpMessageHandler` to serve a *queue* of responses (paging tests need
"page 1, then page 2"). Keep the old surface so Stage 2 tests don't change (📖):

```csharp
public sealed class FakeHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new(responses);

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    public HttpRequestMessage? LastRequest => Requests.Count > 0 ? Requests[^1] : null;
    public string? LastRequestBody => RequestBodies.Count > 0 ? RequestBodies[^1] : null;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responses.Dequeue();
    }
}
```

New `StravaClientTests` (🧩): `GetActivities_StopsAfterAShortPage` (queue a full page of 200 —
generate the JSON in a loop — then a short one; assert exactly 2 requests) ·
`GetActivities_SendsBearerTokenAndAfterEpoch` (assert on `LastRequest`) ·
`GetActivities_MapsAllFields`.

**(b)** The handler tests. Two helpers (📖):

```csharp
// Sync/TestDb.cs — a real AppDbContext on a throwaway in-memory SQLite database.
public static class TestDb
{
    public static (AppDbContext Db, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();   // the database lives exactly as long as this connection stays open

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();             // builds the schema straight from the model
        return (db, connection);
    }
}
```

```csharp
// Sync/FakeStravaClient.cs — you control exactly what "Strava" returns.
public sealed class FakeStravaClient : IStravaClient
{
    public List<StravaActivitySummary> Activities { get; set; } = [];
    public StravaTokens? RefreshResult { get; set; }
    public int RefreshCallCount { get; private set; }

    public string BuildAuthorizationUrl() => "https://example.test/authorize";

    public Task<StravaAuthorization> ExchangeCodeAsync(string code, CancellationToken ct)
        => throw new NotSupportedException("Not needed in sync tests.");

    public Task<StravaTokens> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        RefreshCallCount++;
        return Task.FromResult(RefreshResult
            ?? throw new InvalidOperationException("Set RefreshResult first."));
    }

    public Task<IReadOnlyList<StravaActivitySummary>> GetActivitiesAsync(
        string accessToken, DateTimeOffset after, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<StravaActivitySummary>>(Activities);
}
```

One full example test (📖) — the rest reuse its setup:

```csharp
public sealed class SyncActivitiesCommandHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly FakeStravaClient _strava = new();

    public SyncActivitiesCommandHandlerTests() => (_db, _connection) = TestDb.Create();

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();   // closing the connection deletes the in-memory database
    }

    private async Task<User> SeedConnectedUserAsync(DateTimeOffset tokenExpiresAt)
    {
        var user = User.Create(67890, "Viv M");
        user.ConnectStrava("access", "refresh", tokenExpiresAt, "activity:read_all");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task FirstSync_StoresOnlyQualifyingActivities()
    {
        await SeedConnectedUserAsync(tokenExpiresAt: DateTimeOffset.UtcNow.AddHours(2));
        _strava.Activities =
        [
            new(1, "Morning run", "Run",        DateTimeOffset.UtcNow.AddDays(-1), 8_000, 120),
            new(2, "Treadmill",   "VirtualRun", DateTimeOffset.UtcNow.AddDays(-1), 5_000,   0),
        ];

        var result = await new SyncActivitiesCommandHandler(_db, _strava)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);

        Assert.Equal(2, result.Fetched);
        Assert.Equal(1, result.Added);
        var stored = Assert.Single(_db.Activities);
        Assert.Equal(1, stored.StravaActivityId);
        Assert.Equal("Run", stored.SportType);
    }

    // 🧩 SecondSync_AddsNothingAndDuplicatesNothing   (run Handle twice; Added == 0; still 1 row)
    // 🧩 Sync_UpdatesAnActivityThatChangedOnStrava    (re-sync with a renamed activity)
    // 🧩 Sync_RefreshesAnExpiringTokenFirst           (expiry in 1 min; RefreshCallCount == 1;
    //                                                  user's stored AccessToken is the new one)
    // 🧩 Sync_WithoutAConnection_Throws
}
```

(Constructor argument order for the handler is whatever *you* declare — adjust.)

**Checkpoint:** `dotnet test` — everything green.

### Step 6 — Api: `SyncController` 🧩

`POST /api/sync`, no body, sends the command, `Ok(result)`. You've written two controllers —
no snippet. Remember `[ProducesResponseType]` for the OpenAPI doc.

**Checkpoint:** `dotnet build`, run the AppHost.

### Step 7 — The moment of truth 🤝

1. AppHost running → open Scalar (`/scalar/v1` on the api resource) → execute `POST /api/sync`.
   (Or PowerShell: `Invoke-RestMethod -Method Post http://localhost:5159/api/sync`)
2. First run: `added` = however many qualifying activities you've logged in 30 days.
3. **Run it again: `added` must be 0.** That's the stage's whole promise, kept.
4. Spot-check the data against your Strava feed:

   ```bash
   docker exec -it <postgres-container> psql -U postgres -d loopquestdb \
     -c "select \"StravaActivityId\", \"Name\", \"SportType\", \"DistanceMeters\" from activities"
   ```

5. While you're there, sanity-check `StartDateLocal` for a run whose local start time you remember
   (see pitfall #1).

---

## 6. Strava API cheat sheet

| | |
|---|---|
| Endpoint | `GET https://www.strava.com/api/v3/athlete/activities` |
| Auth | `Authorization: Bearer <access token>` header |
| Params | `after` (epoch seconds, exclusive), `page` (1-based), `per_page` (max 200; default only 30) |
| Response | JSON array of activity summaries; fields we use: `id`, `name`, `sport_type`, `start_date_local`, `distance` (m), `total_elevation_gain` (m) |
| Rate limits | Per-15-minutes + per-day caps per app (generous for one athlete; our sync ≈ 1 request). A `429` means back off — a Stage 9 concern, not yours today. |

Sample element, for canned test JSON:

```json
{
  "id": 1234567890,
  "name": "Lunch Run",
  "sport_type": "Run",
  "start_date": "2026-06-08T05:30:00Z",
  "start_date_local": "2026-06-08T07:30:00Z",
  "distance": 8012.3,
  "total_elevation_gain": 142.0
}
```

Keep <https://developers.strava.com/docs/reference/#api-Activities> open while implementing; trust
it over this doc if they disagree.

## 7. Pitfalls (each is a real bug waiting)

1. **`start_date_local` lies about its `Z`.** Strava marks it UTC (`…T07:30:00Z`) but the clock
   digits are *local wall time* (the 05:30 UTC run above started at 07:30 in Stockholm). For us
   this is exactly right — week bucketing uses wall-clock local time — so store it as-is and do
   **no** timezone conversion on it. Just don't be surprised by the fake `Z`. Verify against a run
   you remember in Step 7.
2. **Persist rotated tokens immediately** — refresh, save, *then* fetch. A crash between refresh
   and save otherwise strands you with a dead refresh token (fix: reconnect via Stage 2).
3. **Stale `Connection` reference after refresh.** `RefreshTokens` *replaces* the owned object;
   any local variable captured before it now holds the dead token. Re-read `user.Connection`.
4. **Paging stop condition.** Stop on `batch.Count < PageSize`, and always increment `page` —
   the classic infinite loop is re-fetching page 1 forever.
5. **`per_page` defaults to 30.** Forget the parameter and a big month quietly costs 4× the requests.
6. **SQLite in-memory dies with its connection.** `TestDb` opens the connection and hands it to you;
   dispose it at test end, not before. (And `EnsureCreated` builds the schema from the model
   directly — migrations don't run in these tests, which is fine.)
7. **`sport_type`, not `type`.** Strava has a legacy `type` field; doc 02 and we use `sport_type`.
8. **Don't log tokens** — same rule as Stage 2, now with more call sites.

## 8. Definition of Done

- [ ] `dotnet build` — no new warnings/errors
- [ ] `dotnet test` — green: new Domain tests + client paging tests + handler tests
- [ ] `POST /api/sync` pulls your real activities; rows match your Strava feed
- [ ] **Second `POST /api/sync` returns `added: 0` and the row count is unchanged**
- [ ] Migration has the unique index on `StravaActivityId` and the FK to `users`
- [ ] Application gained no new package references; dependency rule intact
- [ ] Reviewed, then merged: `git switch dev && git merge --no-ff feature/stage-3-sync-activities`

## 9. Explicitly out of scope (resist the urge)

- **Scheduled background sync** — Stage 9 (this command will be *called by* it, unchanged).
- **Webhooks** — deferred for v1 entirely; polling wins on simplicity.
- **Week math / summing totals** — Stages 4–5. Today we only *store* faithfully.
- **Sync-state bookkeeping** (last-sync cursor) — the 30-day lookback + upsert makes it unnecessary.
- **429/back-off handling** — ServiceDefaults' resilience retries transient failures already; smarter
  rate-limit awareness can ride along with Stage 9.
