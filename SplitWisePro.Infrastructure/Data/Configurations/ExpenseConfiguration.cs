using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Amount)
            .IsRequired()
            .HasPrecision(18, 4);

        builder.Property(e => e.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(e => e.ExchangeRate)
            .HasPrecision(18, 8);

        builder.Property(e => e.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(e => e.SplitType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.ReceiptUrl)
            .HasMaxLength(2048);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.AiRawInput)
            .HasMaxLength(1000);

        builder.Property(e => e.SyncStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        // ── Indexes ────────────────────────────────────────────────────

        builder.HasIndex(e => e.GroupId)
            .HasDatabaseName("IX_Expenses_GroupId");

        builder.HasIndex(e => e.PaidByUserId)
            .HasDatabaseName("IX_Expenses_PaidByUserId");

        builder.HasIndex(e => e.ExpenseDate)
            .HasDatabaseName("IX_Expenses_ExpenseDate");

        builder.HasIndex(e => e.Category)
            .HasDatabaseName("IX_Expenses_Category");

        builder.HasIndex(e => new { e.GroupId, e.ExpenseDate })
            .HasDatabaseName("IX_Expenses_GroupId_ExpenseDate");

        // ── Relationships ──────────────────────────────────────────────

        builder.HasOne(e => e.Group)
            .WithMany(g => g.Expenses)
            .HasForeignKey(e => e.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PaidByUser)
            .WithMany(u => u.PaidExpenses)
            .HasForeignKey(e => e.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
