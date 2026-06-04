using LoopQuest.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoopQuest.Infrastructure.Persistence;

/// <summary>
/// Applies any pending EF Core migrations and seeds the loop library at startup.
///
/// NOTE: migrating automatically on startup is convenient for a single-user MVP and keeps local dev
/// and integration tests frictionless. For a real production deployment you would instead run migrations
/// as a discrete step (a dedicated migration job / one-off service) so that app instances never race to
/// migrate. That move is tracked in docs/03-roadmap.md.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("LoopQuest.DatabaseInitializer");

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        await LoopSeeder.SeedAsync(db, logger, cancellationToken);
    }
}
