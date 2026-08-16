namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents a trip — a budget-tracked event where members log expenses.
/// Members join via a QR code or unique trip code. One member is the Admin.
/// </summary>
public class Trip : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Destination { get; set; }
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }

    /// <summary>Unique 8-character code users enter to join.</summary>
    public string TripCode { get; set; } = string.Empty;

    /// <summary>FK to the admin user who created and manages this trip.</summary>
    public Guid AdminUserId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public User AdminUser { get; set; } = null!;
    public ICollection<TripMember> Members { get; set; } = new List<TripMember>();
    public ICollection<TripExpense> Expenses { get; set; } = new List<TripExpense>();
}
