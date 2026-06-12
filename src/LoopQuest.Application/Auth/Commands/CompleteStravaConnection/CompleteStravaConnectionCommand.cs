using MediatR;

namespace LoopQuest.Application.Auth.Commands.CompleteStravaConnection;

/// <summary>
/// Connect steps 4–6: carries what Strava put on the callback URL — the one-time Code we trade for
/// tokens, and the Scope the athlete granted (may be absent, e.g. if Strava omits it). Handling it
/// ends with a user row holding tokens; the sender gets back a safe StravaConnectionDto summary.
/// </summary>
public sealed record CompleteStravaConnectionCommand(
    string Code,
    string? Scope) : IRequest<StravaConnectionDto>;
