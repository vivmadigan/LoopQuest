using System.Reflection;
using LoopQuest.Application.Common.Interfaces;
using LoopQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Infrastructure.Persistence;

/// <summary>
/// The EF Core database context. It implements <see cref="IAppDbContext"/> so the Application layer
/// can depend on the abstraction while Infrastructure owns the concrete PostgreSQL mapping.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Loop> Loops => Set<Loop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in this assembly (e.g. LoopConfiguration),
        // keeping mapping code out of this class.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
