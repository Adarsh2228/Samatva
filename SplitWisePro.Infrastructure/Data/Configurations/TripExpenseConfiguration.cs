using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class TripExpenseConfiguration : IEntityTypeConfiguration<TripExpense>
{
    public void Configure(EntityTypeBuilder<TripExpense> builder)
    {
        builder.ToTable("TripExpenses");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("INR");
        builder.Property(e => e.ScreenshotData)
            .HasColumnName("ScreenshotData")
            .HasColumnType("TEXT"); // supports large base64 strings
        builder.Property(e => e.Category).HasMaxLength(100).HasDefaultValue("General");
        builder.Property(e => e.RejectionReason).HasMaxLength(1000);

        builder.HasOne(e => e.AddedByUser)
            .WithMany()
            .HasForeignKey(e => e.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RejectedByUser)
            .WithMany()
            .HasForeignKey(e => e.RejectedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TripId).HasDatabaseName("IX_TripExpenses_TripId");
        builder.HasIndex(e => e.SpentAt).HasDatabaseName("IX_TripExpenses_SpentAt");
    }
}
