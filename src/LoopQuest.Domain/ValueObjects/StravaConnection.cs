using System;
using System.Collections.Generic;
using System.Text;

namespace LoopQuest.Domain.ValueObjects;

// Domain/ValueObjects/StravaConnection.cs
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
}
