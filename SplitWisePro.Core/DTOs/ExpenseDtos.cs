using System.ComponentModel.DataAnnotations;
using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.DTOs;

// ── Create Expense ─────────────────────────────────────────────────

public class CreateExpenseRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "INR";

    public ExpenseCategory Category { get; set; } = ExpenseCategory.General;
    public SplitType SplitType { get; set; } = SplitType.Equal;
    public DateTime? ExpenseDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public string? ReceiptUrl { get; set; }

    /// <summary>
    /// Split details per user. Required for Exact, Percentage, and Shares split types.
    /// For Equal splits, if empty, all group members are included.
    /// </summary>
    public List<SplitDetailRequest> Splits { get; set; } = new();
}

public class SplitDetailRequest
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// For Exact: the absolute amount.
    /// For Percentage: 0-100.
    /// For Shares: number of shares.
    /// For Equal: ignored.
    /// </summary>
    public decimal? Value { get; set; }
}

// ── Update Expense ─────────────────────────────────────────────────

public class UpdateExpenseRequest
{
    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Amount { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; }

    public ExpenseCategory? Category { get; set; }
    public SplitType? SplitType { get; set; }
    public DateTime? ExpenseDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<SplitDetailRequest>? Splits { get; set; }
}

// ── Expense Response ───────────────────────────────────────────────

public class ExpenseDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid PaidByUserId { get; set; }
    public string PaidByDisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal ExchangeRate { get; set; }
    public string Category { get; set; } = "General";
    public string SplitType { get; set; } = "Equal";
    public DateTime ExpenseDate { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ExpenseSplitDto> Splits { get; set; } = new();
}

public class ExpenseSplitDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public decimal OwedAmount { get; set; }
    public decimal? ShareValue { get; set; }
    public bool IsSettled { get; set; }
}

// ── Balance Summary ────────────────────────────────────────────────

public class BalanceDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal NetBalance { get; set; }
    public string Currency { get; set; } = "INR";
}

public class DebtSimplificationDto
{
    public Guid FromUserId { get; set; }
    public string FromDisplayName { get; set; } = string.Empty;
    public Guid ToUserId { get; set; }
    public string ToDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
}
