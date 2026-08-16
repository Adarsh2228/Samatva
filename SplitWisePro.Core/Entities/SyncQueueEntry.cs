using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.Entities;

/// <summary>
/// Represents a queued sync operation for offline-first architecture.
/// When a user performs a CRUD action offline, it is recorded here and
/// replayed against the server when connectivity is restored.
/// </summary>
public class SyncQueueEntry : BaseEntity
{
    /// <summary>FK to the user who initiated the operation.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The type of entity being synced (e.g., "Expense", "Settlement", "Group").
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>The ID of the entity being synced.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The operation type: "Create", "Update", "Delete".</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON payload of the entity at the time of the operation.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Current sync status.</summary>
    public SyncStatus Status { get; set; } = SyncStatus.Pending;

    /// <summary>Number of retry attempts made.</summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>Maximum retries before marking as Failed.</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>Error message from the last failed attempt.</summary>
    public string? LastError { get; set; }

    /// <summary>Timestamp of the last sync attempt.</summary>
    public DateTime? LastAttemptAt { get; set; }

    // ── Navigation Properties ──────────────────────────────────────────

    public User User { get; set; } = null!;
}
