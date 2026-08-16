using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(g => g.Description)
            .HasMaxLength(1000);

        builder.Property(g => g.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(g => g.DefaultCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("INR");

        builder.Property(g => g.GuestLinkToken)
            .HasMaxLength(2048);

        builder.HasIndex(g => g.Name)
            .HasDatabaseName("IX_Groups_Name");

        builder.HasIndex(g => g.IsArchived)
            .HasDatabaseName("IX_Groups_IsArchived");
    }
}
