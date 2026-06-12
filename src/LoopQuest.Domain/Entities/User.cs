using LoopQuest.Domain.ValueObjects;

namespace LoopQuest.Domain.Entities;

/// <summary>
/// An athlete playing LoopQuest, identified by their Strava athlete id. Once they approve the app
/// on Strava, their API tokens live here too (Connection — stays null until the first connect).
/// </summary>
public class User
{
    private User() { }                                // EF

    public Guid Id { get; private set; }
    public long StravaAthleteId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string TimeZoneId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public StravaConnection? Connection { get; private set; }   // null until first connect

    public static User Create(long stravaAthleteId, string displayName,
        string timeZoneId = "Europe/Stockholm")
    {
        if (stravaAthleteId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stravaAthleteId), stravaAthleteId, "Strava athlete id must be positive.");
        }
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A user must have a name.", nameof(displayName));
        }
        return new User
        {
            Id = Guid.NewGuid(),
            StravaAthleteId = stravaAthleteId,
            DisplayName = displayName.Trim(),
            TimeZoneId = timeZoneId,
            CreatedAt = DateTimeOffset.UtcNow

        };
    }

    /// <summary>Stores (or replaces) the Strava tokens — serves the first connect, reconnects, and
    /// later token refreshes alike, which is what keeps reconnecting idempotent.</summary>
    public void ConnectStrava(string accessToken, string refreshToken,
        DateTimeOffset expiresAt, string scope)
    {
        Connection = StravaConnection.Create(accessToken, refreshToken, expiresAt, scope);
    }
}
