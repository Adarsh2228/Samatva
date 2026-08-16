using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActivityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Message)
            .IsRequired()
            .HasMaxLength(1000);

        // ── Indexes ────────────────────────────────────────────────────

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_ActivityLogs_UserId");

        builder.HasIndex(a => a.GroupId)
            .HasDatabaseName("IX_ActivityLogs_GroupId");

        builder.HasIndex(a => new { a.UserId, a.IsRead })
            .HasDatabaseName("IX_ActivityLogs_UserId_IsRead");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("IX_ActivityLogs_CreatedAt");

        // ── Relationships ──────────────────────────────────────────────

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Group)
            .WithMany()
            .HasForeignKey(a => a.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
