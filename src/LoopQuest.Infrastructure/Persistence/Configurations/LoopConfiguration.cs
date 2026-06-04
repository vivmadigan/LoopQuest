using LoopQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoopQuest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Loop"/> entity to its table. Enums are stored as readable strings rather than
/// integers so the database is easy to inspect and resistant to enum re-ordering.
/// </summary>
public sealed class LoopConfiguration : IEntityTypeConfiguration<Loop>
{
    public void Configure(EntityTypeBuilder<Loop> builder)
    {
        builder.ToTable("loops");

        builder.HasKey(loop => loop.Id);

        builder.Property(loop => loop.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(loop => loop.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(loop => loop.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(loop => loop.Tier)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(loop => loop.RealWorldReference)
            .HasMaxLength(200);

        builder.Property(loop => loop.ImageUrl)
            .HasMaxLength(500);

        // The picker only ever queries active loops, so index that flag.
        builder.HasIndex(loop => loop.IsActive);
    }
}
