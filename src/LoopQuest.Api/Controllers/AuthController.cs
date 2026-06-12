using LoopQuest.Application.Auth.Commands.CompleteStravaConnection;
using LoopQuest.Application.Auth.Queries.GetStravaAuthUrl;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoopQuest.Api.Controllers;

[ApiController]
[Route("auth/strava")]                      // note: not under /api — per the roadmap's paths
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpGet("connect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        var url = await sender.Send(new GetStravaAuthUrlQuery(), cancellationToken);
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        string? code, string? scope, string? error, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Problem(
                title: "Strava authorization failed",
                detail: $"Strava returned an error: {error}",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (string.IsNullOrEmpty(code))
        {
            return Problem(
                title: "Strava authorization failed",
                detail: "The callback did not include an authorization code.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        var result = await sender.Send(new CompleteStravaConnectionCommand(code, scope), cancellationToken);
        return Ok(result);
    }
}
