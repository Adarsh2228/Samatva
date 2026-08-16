using System.ComponentModel.DataAnnotations;

namespace SplitWisePro.Core.DTOs;

// ── Trip Chat (AI) ─────────────────────────────────────────────────
public class TripChatRequest
{
    [Required, MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}

// ── Create Trip ────────────────────────────────────────────────────
public class CreateTripRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Destination { get; set; }

    [Required, Range(1, double.MaxValue)]
    public decimal Budget { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "INR";

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
}

// ── Trip Response ──────────────────────────────────────────────────
public class TripDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Destination { get; set; }
    public decimal Budget { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal RemainingBudget { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string TripCode { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public Guid AdminUserId { get; set; }
    public string AdminDisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TripMemberDto> Members { get; set; } = new();
}

public class TripMemberDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsAdmin { get; set; }
}

// ── Add Trip Expense ───────────────────────────────────────────────
public class AddTripExpenseRequest
{
    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required, Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "INR";

    public DateTime SpentAt { get; set; } = DateTime.UtcNow;

    public string? ScreenshotData { get; set; }

    [MaxLength(100)]
    public string Category { get; set; } = "General";
}

// ── Trip Expense Response ──────────────────────────────────────────
public class TripExpenseDto
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid AddedByUserId { get; set; }
    public string AddedByDisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime SpentAt { get; set; }
    public string? ScreenshotData { get; set; }
    public string Category { get; set; } = "General";
    public bool IsRejected { get; set; }
    public string? RejectionReason { get; set; }
    public string? RejectedByDisplayName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Join Trip ──────────────────────────────────────────────────────
public class JoinTripRequest
{
    [Required, MaxLength(20)]
    public string TripCode { get; set; } = string.Empty;
}

// ── Reject Expense ─────────────────────────────────────────────────
public class RejectTripExpenseRequest
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

// ── Update Budget ──────────────────────────────────────────────────
public class UpdateTripBudgetRequest
{
    [Required, Range(1, double.MaxValue)]
    public decimal Budget { get; set; }
}
