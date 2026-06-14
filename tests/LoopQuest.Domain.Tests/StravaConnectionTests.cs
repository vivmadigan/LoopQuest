using LoopQuest.Domain.ValueObjects;

namespace LoopQuest.Domain.Tests;

public class StravaConnectionTests
{
    // A fixed "now" so the clock sits exactly where each test places it.
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsExpiredOrExpiringWithin_TrueWhenInsideBuffer()
    {
        // Dies in 2 minutes; our buffer is 5 → treat as "expiring soon", refresh now.
        var connection = StravaConnection.Create("access", "refresh", Now.AddMinutes(2), "activity:read_all");

        Assert.True(connection.IsExpiredOrExpiringWithin(TimeSpan.FromMinutes(5), Now));
    }

    [Fact]
    public void IsExpiredOrExpiringWithin_FalseWhenComfortablyValid()
    {
        // Dies in 3 hours; well outside the 5-minute buffer → still good to use.
        var connection = StravaConnection.Create("access", "refresh", Now.AddHours(3), "activity:read_all");

        Assert.False(connection.IsExpiredOrExpiringWithin(TimeSpan.FromMinutes(5), Now));
    }

    [Fact]
    public void IsExpiredOrExpiringWithin_TrueWhenAlreadyExpired()
    {
        // Died an hour ago → definitely needs a refresh.
        var connection = StravaConnection.Create("access", "refresh", Now.AddHours(-1), "activity:read_all");

        Assert.True(connection.IsExpiredOrExpiringWithin(TimeSpan.FromMinutes(5), Now));
    }
}
