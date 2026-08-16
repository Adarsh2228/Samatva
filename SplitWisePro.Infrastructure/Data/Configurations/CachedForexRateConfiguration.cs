using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class CachedForexRateConfiguration : IEntityTypeConfiguration<CachedForexRate>
{
    public void Configure(EntityTypeBuilder<CachedForexRate> builder)
    {
        builder.ToTable("CachedForexRates");

        builder.HasKey(fr => fr.Id);

        builder.Property(fr => fr.BaseCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(fr => fr.TargetCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(fr => fr.Rate)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(fr => fr.Source)
            .IsRequired()
            .HasMaxLength(100);

        // Unique constraint: one rate per currency pair per date
        builder.HasIndex(fr => new { fr.BaseCurrency, fr.TargetCurrency, fr.RateDate })
            .IsUnique()
            .HasDatabaseName("IX_CachedForexRates_Pair_Date");

        builder.HasIndex(fr => fr.RateDate)
            .HasDatabaseName("IX_CachedForexRates_RateDate");
    }
}
