namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents a group of users sharing expenses (e.g., "Trip to Goa", "Flatmates").
/// </summary>
public class Group : BaseEntity
{
    /// <summary>Display name of the group.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description or purpose of the group.</summary>
    public string? Description { get; set; }

    /// <summary>URL to the group's cover/avatar image.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>ISO 4217 default currency for this group.</summary>
    public string DefaultCurrency { get; set; } = "INR";

    /// <summary>
    /// Secure guest link token (signed JWT) for read-only access without login.
    /// Null if no guest link has been generated.
    /// </summary>
    public string? GuestLinkToken { get; set; }

    /// <summary>Expiry time for the guest link token.</summary>
    public DateTime? GuestLinkExpiresAt { get; set; }

    /// <summary>Short 6-char alphanumeric invite code for registered users to join.</summary>
    public string? InviteCode { get; set; }

    /// <summary>Expiry time for the invite code.</summary>
    public DateTime? InviteCodeExpiresAt { get; set; }

    /// <summary>Whether the group has been archived (no new expenses).</summary>
    public bool IsArchived { get; set; } = false;

    // ── Navigation Properties ──────────────────────────────────────────

    /// <summary>Members of this group (via join entity).</summary>
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();

    /// <summary>Expenses within this group.</summary>
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    /// <summary>Settlements within this group.</summary>
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}
