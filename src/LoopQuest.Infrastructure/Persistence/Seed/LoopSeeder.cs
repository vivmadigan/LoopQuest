using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoopQuest.Infrastructure.Persistence.Seed;

/// <summary>
/// Populates the loop library. Idempotent: it only seeds when the table is empty, so it is safe to run
/// on every startup.
/// </summary>
public static class LoopSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await db.Loops.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Loop library already seeded; skipping.");
            return;
        }

        logger.LogInformation("Seeding {Count} loops into an empty library.", LoopSeedData.Loops.Count);
        db.Loops.AddRange(LoopSeedData.Loops);
        await db.SaveChangesAsync(cancellationToken);
    }
}
