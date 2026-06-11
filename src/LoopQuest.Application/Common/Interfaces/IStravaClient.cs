using System;
using System.Collections.Generic;
using System.Text;

namespace LoopQuest.Application.Common.Interfaces;

/// <summary>
/// Everything LoopQuest needs Strava to do, written as an interface so the rest of the app never
/// touches HTTP or JSON. The real implementation (StravaClient, in Infrastructure) is what the DI
/// container hands to any handler whose constructor asks for an IStravaClient.
/// </summary>
public interface IStravaClient
{
    /// <summary>Connect step 1: builds the strava.com permission-page URL we redirect the user's browser to.</summary>
    string BuildAuthorizationUrl();

    /// <summary>Connect step 6: trades the one-time code from the callback for real tokens + who the athlete is.</summary>
    Task<StravaAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Access tokens die after ~6h; this swaps the refresh token for fresh ones (used from Stage 3).
    /// Strava ROTATES refresh tokens — always store the new one.</summary>
    Task<StravaTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

/// <summary>The three values we must store to call Strava later: the key, the key-renewer, and when the key dies.</summary>
public sealed record StravaTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>What a successful first connect returns: who the athlete is, plus their tokens. (A refresh
/// returns only tokens — that's why there are two records instead of one with empty holes.)</summary>
public sealed record StravaAuthorization(long AthleteId, string AthleteDisplayName, StravaTokens Tokens);
