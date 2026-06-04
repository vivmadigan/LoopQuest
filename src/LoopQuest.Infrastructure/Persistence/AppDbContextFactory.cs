using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoopQuest.Infrastructure.Persistence;

/// <summary>
/// Lets the <c>dotnet ef</c> CLI build an <see cref="AppDbContext"/> at design time — for example when
/// running <c>dotnet ef migrations add</c> — without having to start the Aspire AppHost.
///
/// EF only needs <i>a</i> provider and connection string to build the model and generate migration code;
/// it does <b>not</b> connect to this database during <c>migrations add</c>. At runtime the real
/// connection string is injected by Aspire. Override the default with the
/// <c>LOOPQUEST_DESIGN_TIME_CONNECTION</c> environment variable if your local Postgres differs.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("LOOPQUEST_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=loopquestdb;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
