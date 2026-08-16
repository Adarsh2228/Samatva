namespace SplitWisePro.Core.Enums;

/// <summary>
/// Tracks the lifecycle of a settlement payment.
/// </summary>
public enum SettlementStatus
{
    /// <summary>Settlement has been recorded but not confirmed.</summary>
    Pending = 0,

    /// <summary>Settlement has been confirmed by the receiver.</summary>
    Confirmed = 1,

    /// <summary>Settlement was rejected or disputed.</summary>
    Rejected = 2
}
