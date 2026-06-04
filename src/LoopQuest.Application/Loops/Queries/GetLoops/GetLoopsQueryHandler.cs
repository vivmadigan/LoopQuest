using LoopQuest.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Application.Loops.Queries.GetLoops;

/// <summary>
/// Handles <see cref="GetLoopsQuery"/>: reads the active loops from the database and projects them
/// into <see cref="LoopDto"/>. Projection happens in the query (<c>Select</c> before
/// <c>ToListAsync</c>) so EF Core only selects the columns we need.
/// </summary>
public sealed class GetLoopsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetLoopsQuery, IReadOnlyList<LoopDto>>
{
    public async Task<IReadOnlyList<LoopDto>> Handle(GetLoopsQuery request, CancellationToken cancellationToken)
    {
        return await db.Loops
            .AsNoTracking()
            .Where(loop => loop.IsActive)
            .OrderBy(loop => loop.Tier)
            .ThenBy(loop => loop.TargetDistanceMeters)
            .Select(loop => new LoopDto(
                loop.Id,
                loop.Name,
                loop.Description,
                loop.Category.ToString(),
                loop.Tier.ToString(),
                loop.TargetDistanceMeters,
                loop.TargetElevationMeters,
                loop.RealWorldReference,
                loop.ImageUrl))
            .ToListAsync(cancellationToken);
    }
}
