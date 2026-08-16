using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SplitWisePro.Core.Entities;
using SplitWisePro.Core.Interfaces;
using SplitWisePro.Infrastructure.Data;

namespace SplitWisePro.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation using EF Core.
/// Respects global query filters (soft delete) by default.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);
    }

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.NewGuid();

        await _dbSet.AddAsync(entity, ct);
        return entity;
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        foreach (var entity in entities)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();
        }

        await _dbSet.AddRangeAsync(entities, ct);
    }

    public void Update(T entity)
    {
        _dbSet.Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
    }

    public void Delete(T entity)
    {
        // This triggers the soft-delete interceptor in AppDbContext.SaveChangesAsync
        _dbSet.Remove(entity);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(predicate, ct);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(predicate, ct);
    }
}
