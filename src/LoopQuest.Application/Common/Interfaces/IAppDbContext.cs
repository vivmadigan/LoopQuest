using LoopQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Application.Common.Interfaces;

/// <summary>
/// The persistence abstraction the Application layer depends on. It exposes just the data the
/// use cases need, never the concrete EF Core <c>DbContext</c>. Infrastructure implements this on
/// top of EF Core, which keeps the dependency arrow pointing inward (Application knows nothing
/// about PostgreSQL) and makes handlers easy to test with a fake or in-memory context.
/// </summary>
public interface IAppDbContext
{
    DbSet<Loop> Loops { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
