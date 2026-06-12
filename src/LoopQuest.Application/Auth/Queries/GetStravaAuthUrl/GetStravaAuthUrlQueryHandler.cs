using LoopQuest.Application.Common.Interfaces;
using MediatR;

namespace LoopQuest.Application.Auth.Queries.GetStravaAuthUrl;

/// <summary>
/// Answers GetStravaAuthUrlQuery by asking the Strava client to build the URL. No DB, no HTTP,
/// nothing to wait for — Task.FromResult just wraps the ready answer in the Task shape MediatR requires.
/// </summary>
public sealed class GetStravaAuthUrlQueryHandler(IStravaClient stravaClient)
    : IRequestHandler<GetStravaAuthUrlQuery, string>
{
    public Task<string> Handle(GetStravaAuthUrlQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(stravaClient.BuildAuthorizationUrl());
    }
}
