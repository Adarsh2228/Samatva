using System.ComponentModel.DataAnnotations;
using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.DTOs;

// ── AI NLP Request ─────────────────────────────────────────────────

public class AiParseRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}

// ── AI NLP Parsed Response ─────────────────────────────────────────

public class AiParseResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Parsed expense data, null if parsing failed.</summary>
    public ParsedExpenseData? ParsedExpense { get; set; }

    /// <summary>Original raw input for audit trail.</summary>
    public string RawInput { get; set; } = string.Empty;

    /// <summary>Confidence score 0.0 to 1.0 of the NLP parse.</summary>
    public double Confidence { get; set; }
}

public class ParsedExpenseData
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public ExpenseCategory Category { get; set; } = ExpenseCategory.General;
    public SplitType SplitType { get; set; } = SplitType.Equal;

    /// <summary>
    /// Identified payer name or email from the message.
    /// "me" or "I" maps to the requesting user.
    /// </summary>
    public string? PayerIdentifier { get; set; }

    /// <summary>
    /// Specific participants mentioned (names or emails).
    /// Empty means "everyone in the group".
    /// </summary>
    public List<string> Participants { get; set; } = new();

    /// <summary>Specific split values if mentioned.</summary>
    public List<ParsedSplitDetail> SplitDetails { get; set; } = new();
}

public class ParsedSplitDetail
{
    public string ParticipantIdentifier { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
