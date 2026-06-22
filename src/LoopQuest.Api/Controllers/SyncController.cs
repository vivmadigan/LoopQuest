using LoopQuest.Application.Activities.Commands.SyncActivities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoopQuest.Api.Controllers;

/// <summary>Sync endpoint — pulls recent Strava activities into the database. Thin: HTTP in, MediatR out.</summary>
[Route("api/sync")]
[ApiController]
// ISender (MediatR) is injected by DI; the controller holds no logic itself — it just forwards the command.
public sealed class SyncController(ISender sender) : ControllerBase
{
    // Maps POST /api/sync. The method name (SyncActivities) is for OpenAPI/readability, not part of the URL.
    [HttpPost]
    // Tells OpenAPI the success shape, so Scalar and any generated client know what comes back.
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncActivities(CancellationToken cancellationToken)
    {
        // Hand the command to MediatR → the SyncActivitiesCommandHandler does the real work.
        // The cancellation token is tied to the request, so a client disconnect can stop the sync.
        var result = await sender.Send(new SyncActivitiesCommand(), cancellationToken);

        // 200 OK with the SyncResultDto (fetched / qualifying / added / updated counts).
        return Ok(result);
    }
}
