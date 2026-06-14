using LoopQuest.Domain.Entities;

namespace LoopQuest.Domain.Tests;

public class ActivityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_RejectsNonPositiveStravaActivityId(long stravaActivityId)
    {
        Assert.Throws<ArgumentException>(() => Activity.Create(Guid.NewGuid(),
            stravaActivityId, "Viv", "run", DateTimeOffset.Now, 10_000, 200));
    }

    [Fact]
    public void Create_WithValidValues_SetsProperties()
    {
        var userId = Guid.NewGuid();

        var activity = Activity.Create(
            userId: userId,
            stravaActivityId: 12345,
            name: "  Morning run  ",        // deliberate spaces — proves Trim() runs
            sportType: "Run",
            startDateLocal: new DateTimeOffset(2026, 6, 8, 7, 30, 0, TimeSpan.Zero),
            distanceMeters: 10_000,
            elevationGainMeters: 200);

        Assert.NotEqual(Guid.Empty, activity.Id);        // Create generated an Id
        Assert.Equal(userId, activity.UserId);           // the id we passed round-trips
        Assert.Equal(12345, activity.StravaActivityId);
        Assert.Equal("Morning run", activity.Name);      // trimmed, not "  Morning run  "
        Assert.Equal("Run", activity.SportType);
        Assert.Equal(10_000, activity.DistanceMeters);
        Assert.Equal(200, activity.ElevationGainMeters);
        Assert.NotEqual(default, activity.IngestedAt);   // Create stamped the time
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-300)]
    public void Create_RejectsNegativeDistance(long distanceMeters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Activity.Create(Guid.NewGuid(),
            12345, "Viv", "run", DateTimeOffset.Now, distanceMeters, 200));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-500)]
    public void Create_RejectsNegativeElevation(long elevationGainMeters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Activity.Create(Guid.NewGuid(),
            12345, "Viv", "run", DateTimeOffset.Now, 10_000, elevationGainMeters));
    }

    [Fact]
    public void Create_RejectsEmptyUserId()
    {
        Assert.Throws<ArgumentException>(() => Activity.Create(
            userId: Guid.Empty,           // the one bad value under test
            stravaActivityId: 12345,
            name: "Morning run",
            sportType: "Run",
            startDateLocal: DateTimeOffset.Now,
            distanceMeters: 10_000,
            elevationGainMeters: 200));
    }

    [Fact]
    public void UpdateFromSync_ReplacesSyncedFields_AndLeavesIdentityFieldsAlone()
    {
        var activity = Activity.Create(Guid.NewGuid(), 12345, "Old name", "Run",
            new DateTimeOffset(2026, 6, 8, 7, 30, 0, TimeSpan.Zero), 8_000, 100);

        // capture identity before the update
        var originalId = activity.Id;
        var originalIngestedAt = activity.IngestedAt;

        activity.UpdateFromSync("New name", "TrailRun",
            new DateTimeOffset(2026, 6, 9, 6, 0, 0, TimeSpan.Zero), 12_000, 350);

        // synced fields changed
        Assert.Equal("New name", activity.Name);
        Assert.Equal(12_000, activity.DistanceMeters);
        Assert.Equal(350, activity.ElevationGainMeters);

        // identity fields untouched
        Assert.Equal(originalId, activity.Id);
        Assert.Equal(12345, activity.StravaActivityId);
        Assert.Equal(originalIngestedAt, activity.IngestedAt);
    }
}
