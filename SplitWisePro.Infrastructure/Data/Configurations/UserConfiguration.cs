using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(2048);

        builder.Property(u => u.UpiId)
            .HasMaxLength(100);

        builder.Property(u => u.DefaultCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("INR");

        builder.Property(u => u.TimeZone)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Asia/Kolkata");

        builder.Property(u => u.RefreshTokenHash)
            .HasMaxLength(512);

        // Indexes for performance
        builder.HasIndex(u => u.PhoneNumber)
            .HasDatabaseName("IX_Users_PhoneNumber");

        builder.HasIndex(u => u.UpiId)
            .HasDatabaseName("IX_Users_UpiId");
    }
}
