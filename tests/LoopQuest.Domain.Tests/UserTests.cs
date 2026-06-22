using System;
using System.Collections.Generic;
using System.Text;
using LoopQuest.Domain.Entities;

namespace LoopQuest.Domain.Tests;

public class UserTests
{
    [Fact]
    public void ConnectStrava_StoresTheConnection()
    {
        var user = User.Create(123, "Viv");

        user.ConnectStrava("access", "refresh", DateTimeOffset.UtcNow.AddHours(6), "activity:read_all");

        Assert.NotNull(user.Connection);
        Assert.Equal("access", user.Connection!.AccessToken);
    }

    [Fact]
    public void Create_WithValidInput_SetsIdAndDefaults()
    {
        var user = User.Create(123, "Viv");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(123, user.StravaAthleteId);
        Assert.Equal("Viv", user.DisplayName);
        Assert.Equal("Europe/Stockholm", user.TimeZoneId);
        Assert.NotEqual(default, user.CreatedAt);
        Assert.Null(user.Connection);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-23)]
    public void Create_RejectsNonPositiveAthleteId(long invalidId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => User.Create(invalidId, "Viv"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankDisplayName(string blankName)
    {
        Assert.Throws<ArgumentException>(() => User.Create(123, blankName));
    }

    [Fact]
    public void ConnectStrava_ReplacesAnExistingConnection()
    {
        var user = User.Create(123, "Viv");
        user.ConnectStrava("old-access", "old-refresh", DateTimeOffset.UtcNow.AddHours(1), "read");

        user.ConnectStrava("new-access", "new-refresh", DateTimeOffset.UtcNow.AddHours(6), "activity:read_all");

        Assert.NotNull(user.Connection);
        Assert.Equal("new-access", user.Connection!.AccessToken);
        Assert.Equal("new-refresh", user.Connection.RefreshToken);
        Assert.Equal("activity:read_all", user.Connection.Scope);
    }

    [Fact]
    public void RefreshTokens_KeepsTheExistingScope()
    {
        var user = User.Create(123, "Viv");
        user.ConnectStrava("old-access", "old-refresh", DateTimeOffset.UtcNow.AddMinutes(1), "activity:read_all");

        user.RefreshTokens("new-access", "new-refresh", DateTimeOffset.UtcNow.AddHours(6));

        Assert.Equal("new-access", user.Connection!.AccessToken);   // tokens rotated
        Assert.Equal("new-refresh", user.Connection.RefreshToken);
        Assert.Equal("activity:read_all", user.Connection.Scope);   // ...but scope carried forward
    }

    [Fact]
    public void RefreshTokens_ThrowsWhenNotConnected()
    {
        var user = User.Create(123, "Viv");   // never connected — Connection is null

        Assert.Throws<InvalidOperationException>(
            () => user.RefreshTokens("access", "refresh", DateTimeOffset.UtcNow.AddHours(6)));
    }
}
