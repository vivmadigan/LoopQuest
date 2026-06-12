namespace LoopQuest.Application.Auth.Commands.CompleteStravaConnection;

/// <summary>
/// The safe summary the API returns after a connect: who got connected and when their access token
/// expires. Never the tokens themselves, and never the User entity (API rule: DTOs only).
/// </summary>
public sealed record StravaConnectionDto(
    string DisplayName,
    DateTimeOffset ExpiresAt);
