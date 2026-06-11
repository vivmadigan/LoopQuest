using LoopQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoopQuest.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.StravaAthleteId).IsUnique();

        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.TimeZoneId).HasMaxLength(200).IsRequired();

        builder.OwnsOne(u => u.Connection, connection =>
        {
            connection.Property(c => c.AccessToken).HasMaxLength(512).IsRequired();
            connection.Property(c => c.RefreshToken).HasMaxLength(512).IsRequired();
            connection.Property(c => c.Scope).HasMaxLength(200).IsRequired();
        });
    }
}
