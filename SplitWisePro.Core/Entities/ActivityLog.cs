namespace SplitWisePro.Core.Entities;

/// <summary>
/// Stores the user's activity feed / notification history.
/// Used for real-time notifications via SignalR and in-app feed.
/// </summary>
public class ActivityLog : BaseEntity
{
    /// <summary>FK to the user this activity is relevant to.</summary>
    public Guid UserId { get; set; }

    /// <summary>FK to the group where the activity occurred (optional).</summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Type of activity (e.g., "ExpenseAdded", "SettlementConfirmed", "MemberJoined").
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>Human-readable message describing the activity.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional reference to the related entity ID.</summary>
    public Guid? ReferenceEntityId { get; set; }

    /// <summary>Whether the user has read/seen this activity.</summary>
    public bool IsRead { get; set; } = false;

    // ── Navigation Properties ──────────────────────────────────────────

    public User User { get; set; } = null!;
    public Group? Group { get; set; }
}
