using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class SyncQueueEntryConfiguration : IEntityTypeConfiguration<SyncQueueEntry>
{
    public void Configure(EntityTypeBuilder<SyncQueueEntry> builder)
    {
        builder.ToTable("SyncQueue");

        builder.HasKey(sq => sq.Id);

        builder.Property(sq => sq.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sq => sq.Operation)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(sq => sq.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(sq => sq.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(sq => sq.LastError)
            .HasMaxLength(2000);

        // ── Indexes ────────────────────────────────────────────────────

        builder.HasIndex(sq => sq.UserId)
            .HasDatabaseName("IX_SyncQueue_UserId");

        builder.HasIndex(sq => sq.Status)
            .HasDatabaseName("IX_SyncQueue_Status");

        builder.HasIndex(sq => new { sq.Status, sq.CreatedAt })
            .HasDatabaseName("IX_SyncQueue_Status_CreatedAt");

        // ── Relationships ──────────────────────────────────────────────

        builder.HasOne(sq => sq.User)
            .WithMany()
            .HasForeignKey(sq => sq.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
