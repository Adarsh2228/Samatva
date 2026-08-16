using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.Entities;

/// <summary>
/// Join entity linking Users to Groups with role-based access control.
/// </summary>
public class GroupMember : BaseEntity
{
    /// <summary>FK to the group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>FK to the user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Role of the user within this group (Owner, Admin, Member).</summary>
    public GroupRole Role { get; set; } = GroupRole.Member;

    /// <summary>Date the user joined the group.</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the user has left the group but retains historical data.</summary>
    public bool HasLeft { get; set; } = false;

    // ── Navigation Properties ──────────────────────────────────────────

    public Group Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
