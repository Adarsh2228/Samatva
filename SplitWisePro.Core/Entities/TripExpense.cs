namespace SplitWisePro.Core.Entities;

/// <summary>
/// An expense entry logged by a trip member.
/// Includes amount, reason, optional Base64 screenshot, date/time.
/// Admin can soft-delete with a mandatory reason — deleted entries stay visible to all.
/// </summary>
public class TripExpense : BaseEntity
{
    public Guid TripId { get; set; }
    public Guid AddedByUserId { get; set; }

    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime SpentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Base64-encoded screenshot/receipt image (data:image/...;base64,...). Stored directly in DB to avoid external storage costs.</summary>
    public string? ScreenshotData { get; set; }

    /// <summary>Category tag for the spend.</summary>
    public string Category { get; set; } = "General";

    // ── Admin deletion (soft) ──────────────────────────────────────
    /// <summary>True if admin has marked this entry as deleted/rejected.</summary>
    public bool IsRejected { get; set; } = false;

    /// <summary>Mandatory reason admin must provide when rejecting an entry.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>FK to the admin who rejected this entry.</summary>
    public Guid? RejectedByUserId { get; set; }

    public DateTime? RejectedAt { get; set; }

    // Navigation
    public Trip Trip { get; set; } = null!;
    public User AddedByUser { get; set; } = null!;
    public User? RejectedByUser { get; set; }
}
