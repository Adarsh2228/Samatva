using System.Globalization;
using System.Text.RegularExpressions;
using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Enums;
using SplitWisePro.Core.Interfaces;

namespace SplitWisePro.Infrastructure.Services;

/// <summary>
/// Rule-based NLP parser for natural language expense messages.
/// Runs entirely on the server — no external AI APIs needed (zero cost).
/// 
/// Supported patterns:
///   "I paid 800 for dinner, split equally"
///   "Paid ₹1200 for groceries with Rahul and Priya"
///   "800 dinner split 3 ways"
///   "Rent 15000 split between me, Aditya, Sneha"
///   "$50 uber"
/// </summary>
public partial class AiParserService : IAiParserService
{
    // ── Currency Detection Patterns ────────────────────────────────────
    private static readonly Dictionary<string, string> CurrencySymbols = new()
    {
        { "₹", "INR" }, { "rs", "INR" }, { "rs.", "INR" }, { "inr", "INR" }, { "rupees", "INR" },
        { "$", "USD" }, { "usd", "USD" }, { "dollars", "USD" },
        { "€", "EUR" }, { "eur", "EUR" }, { "euros", "EUR" },
        { "£", "GBP" }, { "gbp", "GBP" }, { "pounds", "GBP" }
    };

    // ── Category Keywords ──────────────────────────────────────────────
    private static readonly Dictionary<string, ExpenseCategory> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "food", ExpenseCategory.Food }, { "dinner", ExpenseCategory.Food },
        { "lunch", ExpenseCategory.Food }, { "breakfast", ExpenseCategory.Food },
        { "restaurant", ExpenseCategory.Food }, { "cafe", ExpenseCategory.Food },
        { "pizza", ExpenseCategory.Food }, { "burger", ExpenseCategory.Food },
        { "biryani", ExpenseCategory.Food }, { "chai", ExpenseCategory.Food },
        { "coffee", ExpenseCategory.Food }, { "snacks", ExpenseCategory.Food },

        { "grocery", ExpenseCategory.Groceries }, { "groceries", ExpenseCategory.Groceries },
        { "vegetables", ExpenseCategory.Groceries }, { "fruits", ExpenseCategory.Groceries },
        { "supermarket", ExpenseCategory.Groceries },

        { "uber", ExpenseCategory.Transport }, { "ola", ExpenseCategory.Transport },
        { "cab", ExpenseCategory.Transport }, { "taxi", ExpenseCategory.Transport },
        { "auto", ExpenseCategory.Transport }, { "metro", ExpenseCategory.Transport },
        { "bus", ExpenseCategory.Transport }, { "petrol", ExpenseCategory.Transport },
        { "fuel", ExpenseCategory.Transport }, { "diesel", ExpenseCategory.Transport },
        { "parking", ExpenseCategory.Transport },

        { "movie", ExpenseCategory.Entertainment }, { "movies", ExpenseCategory.Entertainment },
        { "netflix", ExpenseCategory.Entertainment }, { "spotify", ExpenseCategory.Entertainment },
        { "game", ExpenseCategory.Entertainment }, { "concert", ExpenseCategory.Entertainment },
        { "party", ExpenseCategory.Entertainment },

        { "rent", ExpenseCategory.Rent }, { "apartment", ExpenseCategory.Rent },
        { "flat", ExpenseCategory.Rent }, { "housing", ExpenseCategory.Rent },

        { "electricity", ExpenseCategory.Utilities }, { "water", ExpenseCategory.Utilities },
        { "wifi", ExpenseCategory.Utilities }, { "internet", ExpenseCategory.Utilities },
        { "gas", ExpenseCategory.Utilities }, { "bill", ExpenseCategory.Utilities },
        { "recharge", ExpenseCategory.Utilities },

        { "shopping", ExpenseCategory.Shopping }, { "clothes", ExpenseCategory.Shopping },
        { "shoes", ExpenseCategory.Shopping }, { "amazon", ExpenseCategory.Shopping },
        { "flipkart", ExpenseCategory.Shopping },

        { "flight", ExpenseCategory.Travel }, { "hotel", ExpenseCategory.Travel },
        { "train", ExpenseCategory.Travel }, { "trip", ExpenseCategory.Travel },
        { "travel", ExpenseCategory.Travel }, { "vacation", ExpenseCategory.Travel },

        { "medicine", ExpenseCategory.Healthcare }, { "hospital", ExpenseCategory.Healthcare },
        { "doctor", ExpenseCategory.Healthcare }, { "medical", ExpenseCategory.Healthcare },
        { "pharmacy", ExpenseCategory.Healthcare },

        { "books", ExpenseCategory.Education }, { "course", ExpenseCategory.Education },
        { "tuition", ExpenseCategory.Education }, { "college", ExpenseCategory.Education }
    };

    // ── Split Type Keywords ────────────────────────────────────────────
    private static readonly string[] EqualSplitKeywords = 
        ["equally", "equal", "even", "evenly", "split equally", "split even"];

    public Task<AiParseResponse> ParseExpenseMessageAsync(string message, Guid groupId, Guid requestingUserId)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.FromResult(new AiParseResponse
            {
                Success = false,
                ErrorMessage = "Message cannot be empty.",
                RawInput = message ?? string.Empty,
                Confidence = 0
            });
        }

        // Normalize the input
        var input = message.Trim();
        // Remove "@Bot" or similar prefixes
        input = Regex.Replace(input, @"^@\w+\s*", "", RegexOptions.IgnoreCase);

        try
        {
            // 1. Extract amount and currency
            var (amount, currency, inputAfterAmount) = ExtractAmount(input);
            if (amount <= 0)
            {
                return Task.FromResult(new AiParseResponse
                {
                    Success = false,
                    ErrorMessage = "Could not identify the expense amount. Please include a number (e.g., 'I paid 800 for dinner').",
                    RawInput = message,
                    Confidence = 0
                });
            }

            // 2. Extract description (what the expense is for)
            var description = ExtractDescription(inputAfterAmount, input);

            // 3. Detect category from keywords
            var category = DetectCategory(input);

            // 4. Detect split type
            var splitType = DetectSplitType(input);

            // 5. Extract participants
            var participants = ExtractParticipants(input);

            // 6. Detect payer
            var payerIdentifier = DetectPayer(input);

            // 7. Calculate confidence
            var confidence = CalculateConfidence(amount, description, category, participants);

            var result = new AiParseResponse
            {
                Success = true,
                RawInput = message,
                Confidence = confidence,
                ParsedExpense = new ParsedExpenseData
                {
                    Description = string.IsNullOrWhiteSpace(description) ? $"{category} expense" : description,
                    Amount = amount,
                    Currency = currency,
                    Category = category,
                    SplitType = splitType,
                    PayerIdentifier = payerIdentifier,
                    Participants = participants
                }
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AiParseResponse
            {
                Success = false,
                ErrorMessage = $"Failed to parse the message: {ex.Message}",
                RawInput = message,
                Confidence = 0
            });
        }
    }

    // ── Private Helpers ────────────────────────────────────────────────

    private static (decimal amount, string currency, string remainingInput) ExtractAmount(string input)
    {
        var currency = "INR"; // Default for India market

        // Check for currency symbols/keywords first
        foreach (var (symbol, code) in CurrencySymbols)
        {
            if (input.Contains(symbol, StringComparison.OrdinalIgnoreCase))
            {
                currency = code;
                break;
            }
        }

        // Match patterns like: ₹800, $50, 1200, Rs.800, 15,000, 1200.50
        var amountMatch = Regex.Match(input,
            @"(?:₹|rs\.?|\$|€|£)\s*([0-9]{1,3}(?:,?[0-9]{3})*(?:\.[0-9]{1,2})?)|([0-9]{1,3}(?:,?[0-9]{3})*(?:\.[0-9]{1,2})?)\s*(?:₹|rs\.?|rupees?|dollars?|euros?|pounds?)?",
            RegexOptions.IgnoreCase);

        if (!amountMatch.Success) return (0, currency, input);

        var amountStr = amountMatch.Groups[1].Success ? amountMatch.Groups[1].Value : amountMatch.Groups[2].Value;
        amountStr = amountStr.Replace(",", "");

        if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return (0, currency, input);

        var remaining = input.Replace(amountMatch.Value, " ").Trim();
        return (amount, currency, remaining);
    }

    private static string ExtractDescription(string inputAfterAmount, string originalInput)
    {
        // Look for "for <description>" pattern
        var forMatch = Regex.Match(originalInput, @"\bfor\s+(.+?)(?:\s*,|\s+split|\s+with|\s+between|$)", RegexOptions.IgnoreCase);
        if (forMatch.Success)
        {
            var desc = forMatch.Groups[1].Value.Trim();
            // Clean up common trailing words
            desc = Regex.Replace(desc, @"\s+(equally|equal|evenly|even)$", "", RegexOptions.IgnoreCase);
            return CleanDescription(desc);
        }

        // Remove common action words and extract remaining nouns
        var cleaned = Regex.Replace(inputAfterAmount, 
            @"\b(i|me|my|paid|pay|spent|spend|bought|buy|got|split|equally|equal|evenly|even|with|between|and|for|the|a|an)\b",
            " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        return CleanDescription(cleaned);
    }

    private static string CleanDescription(string desc)
    {
        // Remove currency symbols and amounts from description
        desc = Regex.Replace(desc, @"[₹$€£]", "");
        desc = Regex.Replace(desc, @"\b[0-9]{1,3}(?:,?[0-9]{3})*(?:\.[0-9]{1,2})?\b", "");
        desc = Regex.Replace(desc, @"\s+", " ").Trim();

        if (desc.Length > 0)
            desc = char.ToUpper(desc[0]) + desc[1..];

        return desc;
    }

    private static ExpenseCategory DetectCategory(string input)
    {
        var words = Regex.Split(input.ToLowerInvariant(), @"\W+");
        foreach (var word in words)
        {
            if (CategoryKeywords.TryGetValue(word, out var category))
                return category;
        }
        return ExpenseCategory.General;
    }

    private static SplitType DetectSplitType(string input)
    {
        var lower = input.ToLowerInvariant();

        if (EqualSplitKeywords.Any(k => lower.Contains(k)))
            return SplitType.Equal;

        // Check for percentage patterns: "60% and 40%", "split 70-30"
        if (Regex.IsMatch(lower, @"\d+\s*%"))
            return SplitType.Percentage;

        // Default to equal
        return SplitType.Equal;
    }

    private static List<string> ExtractParticipants(string input)
    {
        var participants = new List<string>();

        // Match "with <names>" or "between <names>"
        var withMatch = Regex.Match(input,
            @"(?:with|between)\s+(.+?)(?:\s+split|\s+equally|\s+evenly|$)",
            RegexOptions.IgnoreCase);

        if (withMatch.Success)
        {
            var namesStr = withMatch.Groups[1].Value;
            // Split by commas, "and", "&"
            var names = Regex.Split(namesStr, @"\s*(?:,|and|&)\s*", RegexOptions.IgnoreCase);
            foreach (var name in names)
            {
                var trimmed = name.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) &&
                    !trimmed.Equals("me", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.Equals("i", StringComparison.OrdinalIgnoreCase))
                {
                    participants.Add(trimmed);
                }
            }
        }

        return participants;
    }

    private static string? DetectPayer(string input)
    {
        var lower = input.ToLowerInvariant();

        // "I paid" / "I spent" / "paid by me"
        if (Regex.IsMatch(lower, @"\bi\s+(?:paid|spent|bought|got)\b") ||
            Regex.IsMatch(lower, @"\bpaid\s+by\s+me\b") ||
            lower.StartsWith("paid ") ||
            lower.StartsWith("i "))
        {
            return "me";
        }

        // "<Name> paid" pattern
        var payerMatch = Regex.Match(input, @"(\w+)\s+paid", RegexOptions.IgnoreCase);
        if (payerMatch.Success)
        {
            var payer = payerMatch.Groups[1].Value;
            if (!payer.Equals("i", StringComparison.OrdinalIgnoreCase) &&
                !payer.Equals("we", StringComparison.OrdinalIgnoreCase))
            {
                return payer;
            }
            return "me";
        }

        return "me"; // Default to requesting user
    }

    private static double CalculateConfidence(decimal amount, string description, ExpenseCategory category, List<string> participants)
    {
        var confidence = 0.3; // Base confidence for having an amount

        if (amount > 0) confidence += 0.2;
        if (!string.IsNullOrWhiteSpace(description)) confidence += 0.2;
        if (category != ExpenseCategory.General) confidence += 0.15;
        if (participants.Count > 0) confidence += 0.15;

        return Math.Min(confidence, 1.0);
    }
}
