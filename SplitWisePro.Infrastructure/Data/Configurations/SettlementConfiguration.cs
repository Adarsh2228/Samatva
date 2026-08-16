using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlements");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Amount)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(s => s.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.Notes)
            .HasMaxLength(1000);

        builder.Property(s => s.PaymentMethod)
            .HasMaxLength(50);

        builder.Property(s => s.UpiTransactionId)
            .HasMaxLength(100);

        // ── Indexes ────────────────────────────────────────────────────

        builder.HasIndex(s => s.GroupId)
            .HasDatabaseName("IX_Settlements_GroupId");

        builder.HasIndex(s => s.PayerUserId)
            .HasDatabaseName("IX_Settlements_PayerUserId");

        builder.HasIndex(s => s.ReceiverUserId)
            .HasDatabaseName("IX_Settlements_ReceiverUserId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_Settlements_Status");

        builder.HasIndex(s => new { s.GroupId, s.SettlementDate })
            .HasDatabaseName("IX_Settlements_GroupId_SettlementDate");

        // ── Relationships ──────────────────────────────────────────────

        builder.HasOne(s => s.Group)
            .WithMany(g => g.Settlements)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PayerUser)
            .WithMany(u => u.SettlementsPaid)
            .HasForeignKey(s => s.PayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ReceiverUser)
            .WithMany(u => u.SettlementsReceived)
            .HasForeignKey(s => s.ReceiverUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
