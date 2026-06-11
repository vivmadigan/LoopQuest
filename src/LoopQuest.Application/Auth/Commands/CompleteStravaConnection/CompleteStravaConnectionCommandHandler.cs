using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Application.Auth.Commands.CompleteStravaConnection;

/// <summary>
/// The end of the connect flow: trades the callback code for tokens, finds or creates the user,
/// stores the tokens on them, and returns a safe summary. Running it again for the same athlete
/// updates the same row — reconnecting can never create a duplicate user.
/// </summary>
// The constructor parameters are interfaces (types), not classes or files. At run time the DI
// container builds an instance of whatever class is registered for each interface
// (IAppDbContext → AppDbContext, IStravaClient → StravaClient) and passes those objects in.
// This handler uses them through the interface and never learns which concrete class it got.
public sealed class CompleteStravaConnectionCommandHandler
    (IStravaClient stravaClient, IAppDbContext db)
    : IRequestHandler<CompleteStravaConnectionCommand, StravaConnectionDto>
{
    public async Task<StravaConnectionDto> Handle(CompleteStravaConnectionCommand request, CancellationToken cancellationToken)
    {
        // 1. Trade the one-time code for tokens + the athlete's identity (server-to-server).
        var auth = await stravaClient.ExchangeCodeAsync(request.Code, cancellationToken);

        // 2. Look the athlete up by Strava id — null means they've never connected before.
        var user = await db.Users.SingleOrDefaultAsync(u => u.StravaAthleteId == auth.AthleteId, cancellationToken);

        // 3. First visit: create the user and hand it to EF so SaveChanges will INSERT it.
        if (user == null)
        {
            user = User.Create(auth.AthleteId, auth.AthleteDisplayName);
            db.Users.Add(user);
        }

        // 4. Store (or replace) the tokens; a missing scope is stored as "".
        user.ConnectStrava(auth.Tokens.AccessToken, auth.Tokens.RefreshToken, auth.Tokens.ExpiresAt, request.Scope ?? "");

        // 5. Write every tracked change in one go — an INSERT or an UPDATE on the users row.
        await db.SaveChangesAsync(cancellationToken);

        // Never return the entity or the tokens — just the safe summary.
        return new StravaConnectionDto(
            DisplayName: user.DisplayName,
            ExpiresAt: auth.Tokens.ExpiresAt);
    }
}
