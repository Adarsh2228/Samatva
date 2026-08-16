using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder)
    {
        builder.ToTable("ExpenseSplits");

        builder.HasKey(es => es.Id);

        builder.Property(es => es.OwedAmount)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(es => es.ShareValue)
            .HasPrecision(18, 4);

        // ── Indexes ────────────────────────────────────────────────────

        builder.HasIndex(es => es.ExpenseId)
            .HasDatabaseName("IX_ExpenseSplits_ExpenseId");

        builder.HasIndex(es => es.UserId)
            .HasDatabaseName("IX_ExpenseSplits_UserId");

        builder.HasIndex(es => new { es.ExpenseId, es.UserId })
            .IsUnique()
            .HasDatabaseName("IX_ExpenseSplits_ExpenseId_UserId");

        builder.HasIndex(es => es.IsSettled)
            .HasDatabaseName("IX_ExpenseSplits_IsSettled");

        // ── Relationships ──────────────────────────────────────────────

        builder.HasOne(es => es.Expense)
            .WithMany(e => e.Splits)
            .HasForeignKey(es => es.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade); // Splits die with their expense

        builder.HasOne(es => es.User)
            .WithMany(u => u.ExpenseSplits)
            .HasForeignKey(es => es.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
