using LoopQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Infrastructure.Persistence.Configurations;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activities");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.StravaActivityId).IsUnique();

        builder.Property(a => a.Name).HasMaxLength(300).IsRequired();

        builder.Property(a => a.SportType).HasMaxLength(50).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId);

    }
}
