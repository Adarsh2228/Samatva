namespace SplitWisePro.Core.Entities;

/// <summary>
/// Stores daily cached forex rates to avoid repeated API calls.
/// Rates are fetched once daily from a free forex API and cached server-side.
/// Client-side caching is also implemented in Angular.
/// </summary>
public class CachedForexRate : BaseEntity
{
    /// <summary>Base currency code (e.g., "USD").</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>Target currency code (e.g., "INR").</summary>
    public string TargetCurrency { get; set; } = string.Empty;

    /// <summary>Exchange rate: 1 BaseCurrency = Rate TargetCurrency.</summary>
    public decimal Rate { get; set; }

    /// <summary>Date this rate was fetched (UTC, date only).</summary>
    public DateOnly RateDate { get; set; }

    /// <summary>Source API that provided this rate.</summary>
    public string Source { get; set; } = "ExchangeRate-API";
}
