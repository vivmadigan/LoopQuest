using LoopQuest.Application.Common.Interfaces;
using MediatR;

namespace LoopQuest.Application.Auth.Queries.GetStravaAuthUrl;

/// <summary>
/// Connect step 1: "give me the Strava permission-page URL to send the browser to."
/// Carries no inputs; whoever sends it gets the URL back as a string.
/// </summary>
public sealed record GetStravaAuthUrlQuery : IRequest<string>;

