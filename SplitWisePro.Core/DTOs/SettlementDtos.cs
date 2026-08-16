using System.ComponentModel.DataAnnotations;

namespace SplitWisePro.Core.DTOs;

// ── Create Settlement ──────────────────────────────────────────────

public class CreateSettlementRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    public Guid ReceiverUserId { get; set; }

    [Required, Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "INR";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [MaxLength(100)]
    public string? UpiTransactionId { get; set; }
}

// ── Settlement Response ────────────────────────────────────────────

public class SettlementDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid PayerUserId { get; set; }
    public string PayerDisplayName { get; set; } = string.Empty;
    public Guid ReceiverUserId { get; set; }
    public string ReceiverDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Pending";
    public DateTime SettlementDate { get; set; }
    public string? Notes { get; set; }
    public string? PaymentMethod { get; set; }
    public string? UpiTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── UPI Deep Link ──────────────────────────────────────────────────

public class UpiDeepLinkResponse
{
    public string UpiIntentUrl { get; set; } = string.Empty;
    public string PayeeName { get; set; } = string.Empty;
    public string PayeeUpiId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionNote { get; set; } = string.Empty;
    /// <summary>QR code image URL (for desktop — user scans to pay via mobile UPI app).</summary>
    public string QrCodeUrl { get; set; } = string.Empty;
}

// ── Spending Analytics ─────────────────────────────────────────────

public class SpendingAnalyticsDto
{
    public decimal TotalSpent { get; set; }
    public decimal TotalOwed { get; set; }
    public decimal TotalOwedToYou { get; set; }
    public string Currency { get; set; } = "INR";
    public List<CategoryBreakdownDto> CategoryBreakdown { get; set; } = new();
    public List<DailySpendDto> DailySpending { get; set; } = new();
    public List<MemberSpendDto> MemberSpending { get; set; } = new();
    public string TopCategory { get; set; } = string.Empty;
    public decimal AverageExpense { get; set; }
    public int TotalTransactions { get; set; }
}

public class CategoryBreakdownDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class DailySpendDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class MemberSpendDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
    public decimal NetBalance { get; set; }
}

// ── Settlement Breakdown ────────────────────────────────────────────
public class SettlementBreakdownItem
{
    public Guid ExpenseId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal YourShare { get; set; }
    public string Currency { get; set; } = "INR";
    public string PaidBy { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Category { get; set; } = "General";
}

public class SettlementBreakdownResponse
{
    public Guid ReceiverUserId { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string? ReceiverUpiId { get; set; }
    public string? ReceiverPhone { get; set; }
    public decimal TotalOwed { get; set; }
    public string Currency { get; set; } = "INR";
    public List<SettlementBreakdownItem> Breakdown { get; set; } = new();
    public string? UpiIntentUrl { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? WhatsAppUrl { get; set; }
}
