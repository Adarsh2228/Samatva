namespace SplitWisePro.Core.Entities;

/// <summary>Join table for Trip members.</summary>
public class TripMember : BaseEntity
{
    public Guid TripId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Trip Trip { get; set; } = null!;
    public User User { get; set; } = null!;
}
