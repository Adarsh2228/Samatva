namespace SplitWisePro.Core.Entities;

/// <summary>
/// Base entity providing common audit fields for all domain objects.
/// Uses Guid primary keys for offline-first conflict-free ID generation.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary key. Generated client-side (Guid.NewGuid()) to support offline-first creation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>UTC timestamp when the entity was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the entity was last modified. Used for conflict resolution (last-write-wins).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Soft delete flag. Entities are never physically removed.</summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Monotonically increasing version number for optimistic concurrency and sync conflict detection.
    /// </summary>
    public long Version { get; set; } = 1;
}
