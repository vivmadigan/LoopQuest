using System;
using System.Collections.Generic;
using System.Text;

namespace LoopQuest.Domain.ValueObjects;

/// <summary>
/// A user's stored Strava API keys: AccessToken (the key — dies after ~6h), RefreshToken (used to
/// get fresh keys), ExpiresAt (when the key dies), Scope (what we're allowed to read). It has no
/// table of its own — EF saves these as extra columns inside the users table (an "owned" type).
/// </summary>
public class StravaConnection
{
    private StravaConnection() { }                    // EF

    public string AccessToken { get; private set; } = null!;
    public string RefreshToken { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Scope { get; private set; } = null!;

    public static StravaConnection Create(string accessToken, string refreshToken,
    DateTimeOffset expiresAt, string scope)
    {
        if(string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token must not be blank.", nameof(accessToken));
        }
        if(string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token must not be blank.", nameof(refreshToken));
        }

        return new StravaConnection
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            Scope = scope,
        };
    }

    /// <summary>
    /// True if the access token is already expired or will expire within <paramref name="buffer"/> of
    /// <paramref name="now"/>. The handler checks this before each Strava request and refreshes when it
    /// returns true, so a token that's seconds from dying is never sent. <paramref name="now"/> is a
    /// parameter (not read from the clock) so the boundary cases stay unit-testable.
    /// </summary>
    public bool IsExpiredOrExpiringWithin(TimeSpan buffer, DateTimeOffset now)
        => ExpiresAt <= now + buffer;
}
