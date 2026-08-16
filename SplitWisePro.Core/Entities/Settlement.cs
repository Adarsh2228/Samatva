using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents a settlement (payment) between two users within a group.
/// Tracks the lifecycle: Pending → Confirmed/Rejected.
/// </summary>
public class Settlement : BaseEntity
{
    /// <summary>FK to the group where this settlement occurs.</summary>
    public Guid GroupId { get; set; }

    /// <summary>FK to the user making the payment (payer/debtor).</summary>
    public Guid PayerUserId { get; set; }

    /// <summary>FK to the user receiving the payment (payee/creditor).</summary>
    public Guid ReceiverUserId { get; set; }

    /// <summary>Amount being settled.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code for this settlement.</summary>
    public string Currency { get; set; } = "INR";

    /// <summary>Current status of the settlement.</summary>
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

    /// <summary>Date of the settlement.</summary>
    public DateTime SettlementDate { get; set; } = DateTime.UtcNow;

    /// <summary>Optional note (e.g., "Paid via GPay").</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Payment method used (e.g., "UPI", "Cash", "Bank Transfer").
    /// </summary>
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// UPI transaction reference ID if paid via UPI deep link.
    /// </summary>
    public string? UpiTransactionId { get; set; }

    // ── Navigation Properties ──────────────────────────────────────────

    public Group Group { get; set; } = null!;
    public User PayerUser { get; set; } = null!;
    public User ReceiverUser { get; set; } = null!;
}
