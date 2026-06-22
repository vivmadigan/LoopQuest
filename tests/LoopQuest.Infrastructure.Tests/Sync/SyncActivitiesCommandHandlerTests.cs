using LoopQuest.Application.Activities.Commands.SyncActivities;
using LoopQuest.Domain.Entities;
using LoopQuest.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopQuest.Infrastructure.Tests.Sync;

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

        var result = await new SyncActivitiesCommandHandler(_strava, _db)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);

        Assert.Equal(2, result.Fetched);
        Assert.Equal(1, result.Added);
        var stored = Assert.Single(_db.Activities);
        Assert.Equal(1, stored.StravaActivityId);
        Assert.Equal("Run", stored.SportType);
    }

    [Fact]
    public async Task SecondSync_AddsNothingAndDuplicatesNothing()
    {
        await SeedConnectedUserAsync(tokenExpiresAt: DateTimeOffset.UtcNow.AddHours(2));
        _strava.Activities =
        [
            new(1, "Morning run", "Run",        DateTimeOffset.UtcNow.AddDays(-1), 8_000, 120),
            new(2, "Treadmill",   "VirtualRun", DateTimeOffset.UtcNow.AddDays(-1), 5_000,   0),
        ];

        var result = await new SyncActivitiesCommandHandler(_strava, _db)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);
        var result2 = await new SyncActivitiesCommandHandler(_strava, _db)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);

        // First sync adds the one qualifying activity (the VirtualRun is filtered out).
        Assert.Equal(1, result.Added);

        // Second sync: nothing NEW added...
        Assert.Equal(0, result2.Added);

        // ...and no duplicate row — exactly one activity remains. THIS is the idempotency proof.
        Assert.Single(_db.Activities);

    }

    [Fact]
    public async Task Sync_UpdatesAnActivityThatChangedOnStrava()
    {
        await SeedConnectedUserAsync(tokenExpiresAt: DateTimeOffset.UtcNow.AddHours(2));

        // First sync stores the activity exactly as Strava reports it now.
        _strava.Activities =
        [
            new(1, "Morning run", "Run", DateTimeOffset.UtcNow.AddDays(-1), 8_000, 120),
        ];
        await new SyncActivitiesCommandHandler(_strava, _db)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);

        // Now the SAME activity (same Id) comes back changed — renamed, with corrected elevation.
        // Same Id is what makes the handler match the existing row instead of inserting a new one.
        _strava.Activities =
        [
            new(1, "Morning trail run", "Run", DateTimeOffset.UtcNow.AddDays(-1), 8_000, 250),
        ];
        var result = await new SyncActivitiesCommandHandler(_strava, _db)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);

        // It took the UPDATE branch, not INSERT: nothing added, one row updated.
        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Updated);

        // Still a single row, now holding the changed values — the edit overwrote in place.
        var stored = Assert.Single(_db.Activities);
        Assert.Equal("Morning trail run", stored.Name);
        Assert.Equal(250, stored.ElevationGainMeters);
    }

    [Fact]
    public async Task Sync_RefreshesAnExpiringTokenFirst()
    {
        // Token expires in 1 minute — inside the handler's 5-minute buffer, so beat 2 must refresh
        // before it fetches activities.
        await SeedConnectedUserAsync(tokenExpiresAt: DateTimeOffset.UtcNow.AddMinutes(1));

        // What the fake "Strava" hands back when asked to refresh. RefreshAsync throws if this is
        // unset, so providing it is mandatory. No activities needed — this test is only about the token.
        _strava.RefreshResult = new("new_access", "new_refresh", DateTimeOffset.UtcNow.AddHours(6));

        await new SyncActivitiesCommandHandler(_strava, _db)
            .Handle(new SyncActivitiesCommand(), CancellationToken.None);

        // The handler refreshed exactly once...
        Assert.Equal(1, _strava.RefreshCallCount);

        // ...and saved the rotated access token, so re-reading the user shows the new value.
        var stored = Assert.Single(_db.Users);
        Assert.Equal("new_access", stored.Connection!.AccessToken);
    }

    [Fact]
    public async Task Sync_WithoutAConnection_Throws()
    {
        // No user seeded at all → the handler's beat-1 guard rejects the sync with a clear error,
        // rather than crashing further down when it tries to use a token that isn't there.
        var handler = new SyncActivitiesCommandHandler(_strava, _db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SyncActivitiesCommand(), CancellationToken.None));
    }
}
