using SplitWisePro.Core.Enums;

namespace SplitWisePro.Core.DTOs;

// ── Sync Payload (Client → Server) ─────────────────────────────────

public class SyncPushRequest
{
    public List<SyncOperation> Operations { get; set; } = new();
}

public class SyncOperation
{
    public Guid ClientId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // Create, Update, Delete
    public string Payload { get; set; } = string.Empty;   // Serialized JSON
    public DateTime ClientTimestamp { get; set; }
    public long ClientVersion { get; set; }
}

// ── Sync Response (Server → Client) ────────────────────────────────

public class SyncPushResponse
{
    public List<SyncResult> Results { get; set; } = new();
    public DateTime ServerTimestamp { get; set; } = DateTime.UtcNow;
}

public class SyncResult
{
    public Guid ClientId { get; set; }
    public Guid EntityId { get; set; }
    public SyncStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public long ServerVersion { get; set; }
}

// ── Sync Pull (Server → Client) ───────────────────────────────────

public class SyncPullRequest
{
    /// <summary>Last sync timestamp. Server returns all changes after this.</summary>
    public DateTime LastSyncTimestamp { get; set; }
}

public class SyncPullResponse
{
    public List<SyncChangedEntity> Changes { get; set; } = new();
    public DateTime ServerTimestamp { get; set; } = DateTime.UtcNow;
}

public class SyncChangedEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public long ServerVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
}
