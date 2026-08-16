namespace SplitWisePro.Core.Enums;

/// <summary>
/// Defines how an expense is split among group members.
/// </summary>
public enum SplitType
{
    /// <summary>Split equally among all participants.</summary>
    Equal = 0,

    /// <summary>Split by exact amounts specified per participant.</summary>
    Exact = 1,

    /// <summary>Split by percentage specified per participant.</summary>
    Percentage = 2,

    /// <summary>Split by shares (ratio-based).</summary>
    Shares = 3
}
