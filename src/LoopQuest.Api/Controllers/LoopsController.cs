using LoopQuest.Application.Loops.Queries.GetLoops;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoopQuest.Api.Controllers;

/// <summary>
/// The loop library endpoint — the reference vertical slice. The controller is intentionally thin:
/// it translates HTTP to a MediatR request and back. All behaviour lives in the Application handler.
/// </summary>
[ApiController]
[Route("api/loops")]
public sealed class LoopsController(ISender sender) : ControllerBase
{
    /// <summary>Returns the active loop library, for the challenge picker.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LoopDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoopDto>>> GetLoops(CancellationToken cancellationToken)
    {
        var loops = await sender.Send(new GetLoopsQuery(), cancellationToken);
        return Ok(loops);
    }
}
