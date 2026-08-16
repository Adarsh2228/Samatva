using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Destination).HasMaxLength(200);
        builder.Property(t => t.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("INR");
        builder.Property(t => t.Budget).HasPrecision(18, 2);
        builder.Property(t => t.TripCode).IsRequired().HasMaxLength(20);

        builder.HasIndex(t => t.TripCode).IsUnique().HasDatabaseName("IX_Trips_TripCode");

        builder.HasOne(t => t.AdminUser)
            .WithMany()
            .HasForeignKey(t => t.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Members)
            .WithOne(m => m.Trip)
            .HasForeignKey(m => m.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Expenses)
            .WithOne(e => e.Trip)
            .HasForeignKey(e => e.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
