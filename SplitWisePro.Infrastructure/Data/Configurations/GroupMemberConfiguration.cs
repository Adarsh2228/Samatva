using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Infrastructure.Data.Configurations;

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("GroupMembers");

        builder.HasKey(gm => gm.Id);

        // Prevent duplicate memberships
        builder.HasIndex(gm => new { gm.GroupId, gm.UserId })
            .IsUnique()
            .HasDatabaseName("IX_GroupMembers_GroupId_UserId");

        builder.Property(gm => gm.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // ── Relationships ──────────────────────────────────────────────

        builder.HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(gm => gm.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
