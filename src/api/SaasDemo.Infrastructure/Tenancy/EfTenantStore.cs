using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Tenants;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Tenancy;

/// <summary>Looks tenants up in SQL. Tenants are not tenant-owned, so no filter or RLS applies.</summary>
public sealed class EfTenantStore(AppDbContext db) : ITenantStore
{
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        // Slugs are stored lowercase (enforced by TenantSlug), so normalising keeps the index usable.
        var normalized = slug.ToLowerInvariant();
        return db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Slug == normalized, ct);
    }
}
