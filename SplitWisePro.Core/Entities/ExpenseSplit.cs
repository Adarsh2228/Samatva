namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents an individual user's share of an expense.
/// The sum of all splits for an expense should equal the expense amount.
/// </summary>
public class ExpenseSplit : BaseEntity
{
    /// <summary>FK to the parent expense.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>FK to the user who owes this split.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The absolute amount this user owes for this expense (in expense currency).
    /// Calculated at creation time based on the SplitType.
    /// </summary>
    public decimal OwedAmount { get; set; }

    /// <summary>
    /// For Percentage splits: the percentage assigned (0-100).
    /// For Shares splits: the number of shares assigned.
    /// Null for Equal and Exact splits.
    /// </summary>
    public decimal? ShareValue { get; set; }

    /// <summary>Whether this split has been settled (paid back).</summary>
    public bool IsSettled { get; set; } = false;

    // ── Navigation Properties ──────────────────────────────────────────

    public Expense Expense { get; set; } = null!;
    public User User { get; set; } = null!;
}
