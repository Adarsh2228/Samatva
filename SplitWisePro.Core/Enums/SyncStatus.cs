namespace SplitWisePro.Core.Enums;

/// <summary>
/// Tracks the synchronization state of offline-first operations.
/// </summary>
public enum SyncStatus
{
    /// <summary>Created locally, not yet synced to server.</summary>
    Pending = 0,

    /// <summary>Successfully synced to the server.</summary>
    Synced = 1,

    /// <summary>Conflict detected during sync, needs resolution.</summary>
    Conflict = 2,

    /// <summary>Sync failed due to error.</summary>
    Failed = 3
}
