using SplitWisePro.Core.DTOs;

namespace SplitWisePro.Core.Interfaces;

/// <summary>
/// Service for parsing natural language expense inputs into structured data.
/// </summary>
public interface IAiParserService
{
    /// <summary>
    /// Parse a natural language message into a structured expense.
    /// Example: "@Bot I paid 800 for dinner, split equally"
    /// </summary>
    Task<AiParseResponse> ParseExpenseMessageAsync(string message, Guid groupId, Guid requestingUserId);
}
