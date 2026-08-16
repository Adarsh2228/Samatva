namespace SplitWisePro.Core.Interfaces;

/// <summary>
/// Unit of Work pattern for coordinating transactional writes across multiple repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Commit all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Begin an explicit database transaction.</summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Commit the current transaction.</summary>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>Rollback the current transaction.</summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
