using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents an expense entry within a group.
/// Supports multi-currency, multiple split types, receipt attachments, and AI-parsed entries.
/// </summary>
public class Expense : BaseEntity
{
    /// <summary>FK to the group this expense belongs to.</summary>
    public Guid GroupId { get; set; }

    /// <summary>FK to the user who paid for this expense.</summary>
    public Guid PaidByUserId { get; set; }

    /// <summary>Human-readable description of the expense.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Total amount of the expense in the specified currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code (e.g., INR, USD, EUR).</summary>
    public string Currency { get; set; } = "INR";

    /// <summary>
    /// Exchange rate to the group's default currency at the time of creation.
    /// 1.0 if same currency as group default.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>Category for analytics and filtering.</summary>
    public ExpenseCategory Category { get; set; } = ExpenseCategory.General;

    /// <summary>How this expense is split among participants.</summary>
    public SplitType SplitType { get; set; } = SplitType.Equal;

    /// <summary>Date when the expense was incurred (may differ from CreatedAt).</summary>
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    /// <summary>Optional URL or path to the receipt image.</summary>
    public string? ReceiptUrl { get; set; }

    /// <summary>Optional notes or additional context.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// If true, this expense was created via the AI NLP assistant.
    /// Stored for analytics and audit trail.
    /// </summary>
    public bool IsAiGenerated { get; set; } = false;

    /// <summary>
    /// The original natural language input from the AI assistant (for audit).
    /// e.g., "@Bot I paid 800 for dinner, split equally"
    /// </summary>
    public string? AiRawInput { get; set; }

    /// <summary>
    /// Sync status for offline-first architecture.
    /// </summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

    // ── Navigation Properties ──────────────────────────────────────────

    public Group Group { get; set; } = null!;
    public User PaidByUser { get; set; } = null!;

    /// <summary>Individual splits for each participant in this expense.</summary>
    public ICollection<ExpenseSplit> Splits { get; set; } = new List<ExpenseSplit>();
}
