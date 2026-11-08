using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Common;
using SaasDemo.Domain.Tenants;
using SaasDemo.Domain.Users;

namespace SaasDemo.Infrastructure.Persistence;

/// <summary>
/// Application database. Tenant isolation layer 1 of 2 (layer 2 is SQL Row-Level Security):
/// every <see cref="ITenantOwned"/> entity gets a global query filter, and SaveChanges refuses
/// to write tenant-owned rows for any tenant other than the current one.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();

    // Referenced from the query filter: EF re-reads it per query, so one cached model serves all tenants.
    // No resolved tenant -> Guid.Empty -> matches no rows (fail closed).
    private Guid CurrentTenantId => tenantContext.IsResolved ? tenantContext.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => t.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantOwnership();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    private void EnforceTenantOwnership()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (!tenantContext.IsResolved)
            {
                throw new InvalidOperationException(
                    $"Cannot save {entry.Metadata.ClrType.Name}: no tenant is resolved for this request.");
            }

            var tenantId = entry.Property<Guid>(nameof(ITenantOwned.TenantId));

            if (entry.State == EntityState.Added && tenantId.CurrentValue == Guid.Empty)
            {
                tenantId.CurrentValue = tenantContext.TenantId;
            }
            else if (tenantId.CurrentValue != tenantContext.TenantId)
            {
                throw new InvalidOperationException(
                    $"Cannot save {entry.Metadata.ClrType.Name} for another tenant.");
            }
        }
    }
}
