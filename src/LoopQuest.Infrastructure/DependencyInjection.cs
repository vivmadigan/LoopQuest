using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Infrastructure.Persistence;
using LoopQuest.Infrastructure.Strava;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoopQuest.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer. Wires up persistence via the Aspire PostgreSQL
/// integration, which provides the connection string (from the AppHost), connection pooling, retries,
/// health checks, logging and telemetry out of the box.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Must match the database resource name declared in the AppHost
    /// (<c>builder.AddPostgres(...).AddDatabase("loopquestdb")</c>).
    /// </summary>
    public const string DatabaseConnectionName = "loopquestdb";

    public static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>(DatabaseConnectionName);

        // Let the Application layer resolve the same context through its abstraction.
        builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        builder.Services.Configure<StravaOptions>(
        builder.Configuration.GetSection(StravaOptions.SectionName));

        builder.Services.AddHttpClient<IStravaClient, StravaClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.strava.com");
        });
    }
}
