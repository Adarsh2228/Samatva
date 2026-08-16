using Microsoft.EntityFrameworkCore;
using SplitWisePro.Core.Entities;
using SplitWisePro.Infrastructure.Data.Configurations;

namespace SplitWisePro.Infrastructure.Data;

/// <summary>
/// Primary EF Core DbContext for SplitWisePro.
/// Applies entity configurations, global query filters (soft delete), and audit interceptors.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ─────────────────────────────────────────────────────────

    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SyncQueueEntry> SyncQueueEntries => Set<SyncQueueEntry>();
    public DbSet<CachedForexRate> CachedForexRates => Set<CachedForexRate>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripMember> TripMembers => Set<TripMember>();
    public DbSet<TripExpense> TripExpenses => Set<TripExpense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the Configurations folder
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new GroupConfiguration());
        modelBuilder.ApplyConfiguration(new GroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseConfiguration());
        modelBuilder.ApplyConfiguration(new ExpenseSplitConfiguration());
        modelBuilder.ApplyConfiguration(new SettlementConfiguration());
        modelBuilder.ApplyConfiguration(new SyncQueueEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CachedForexRateConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityLogConfiguration());
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        modelBuilder.ApplyConfiguration(new TripMemberConfiguration());
        modelBuilder.ApplyConfiguration(new TripExpenseConfiguration());

        // ── Global Query Filters ───────────────────────────────────────
        // Automatically exclude soft-deleted entities from all queries.
        // Use .IgnoreQueryFilters() to include them when needed.

        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Group>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GroupMember>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ExpenseSplit>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Settlement>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SyncQueueEntry>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ActivityLog>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Trip>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TripMember>().HasQueryFilter(e => !e.IsDeleted);
        // TripExpense: NO global filter — rejected entries must still be visible to all
    }

    /// <summary>
    /// Override SaveChanges to automatically update audit fields.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.Version = 1;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.Version++;
                    // Prevent overwriting CreatedAt on updates
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Convert hard deletes to soft deletes
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.Version++;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
