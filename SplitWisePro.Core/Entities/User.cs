namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents a registered application user.
/// Stores profile info, authentication metadata, and UPI details for the India market.
/// </summary>
public class User : BaseEntity
{
    /// <summary>User's display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Unique email address, used as login identifier.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Bcrypt/Argon2 hashed password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Optional phone number for account recovery and UPI linking.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>URL to the user's avatar image.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// UPI Virtual Payment Address for India market deep linking (e.g., user@upi).
    /// </summary>
    public string? UpiId { get; set; }

    /// <summary>ISO 4217 currency code for the user's default currency (e.g., INR, USD).</summary>
    public string DefaultCurrency { get; set; } = "INR";

    /// <summary>User's preferred timezone (IANA format).</summary>
    public string TimeZone { get; set; } = "Asia/Kolkata";

    /// <summary>Hashed refresh token for JWT rotation.</summary>
    public string? RefreshTokenHash { get; set; }

    /// <summary>Expiry time for the current refresh token.</summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // ── Password Reset OTP ─────────────────────────────────────────────

    /// <summary>BCrypt-hashed 5-digit OTP for password reset.</summary>
    public string? PasswordResetOtpHash { get; set; }

    /// <summary>UTC expiry of the password reset OTP (10 minutes).</summary>
    public DateTime? PasswordResetOtpExpiry { get; set; }

    /// <summary>Number of failed OTP verification attempts (max 5).</summary>
    public int OtpAttempts { get; set; } = 0;

    // ── Navigation Properties ──────────────────────────────────────────

    /// <summary>Groups this user belongs to (via join entity).</summary>
    public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();

    /// <summary>Expenses paid by this user.</summary>
    public ICollection<Expense> PaidExpenses { get; set; } = new List<Expense>();

    /// <summary>Expense splits assigned to this user.</summary>
    public ICollection<ExpenseSplit> ExpenseSplits { get; set; } = new List<ExpenseSplit>();

    /// <summary>Settlements where this user is the payer.</summary>
    public ICollection<Settlement> SettlementsPaid { get; set; } = new List<Settlement>();

    /// <summary>Settlements where this user is the receiver.</summary>
    public ICollection<Settlement> SettlementsReceived { get; set; } = new List<Settlement>();
}
