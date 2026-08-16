using System.Linq.Expressions;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Core.Interfaces;

/// <summary>
/// Generic repository interface providing standard CRUD operations.
/// Implementations live in the Infrastructure layer.
/// </summary>
/// <typeparam name="T">Entity type deriving from BaseEntity.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Get an entity by its primary key.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get all entities (excluding soft-deleted).</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Find entities matching a predicate.</summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>Add a new entity.</summary>
    Task<T> AddAsync(T entity, CancellationToken ct = default);

    /// <summary>Add multiple entities in a batch.</summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Update an existing entity.</summary>
    void Update(T entity);

    /// <summary>Soft-delete an entity.</summary>
    void Delete(T entity);

    /// <summary>Check if any entity matches a predicate.</summary>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>Get the count of entities matching a predicate.</summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
