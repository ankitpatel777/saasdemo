using Microsoft.Extensions.Caching.Memory;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.Infrastructure.Tenancy;

/// <summary>
/// Caches tenant lookups for 5 minutes (absolute), since every tenant-scoped request needs one.
/// Trade-off: deactivating a tenant takes up to 5 minutes to take effect. Misses are not cached,
/// so a newly created tenant is reachable immediately.
/// </summary>
public sealed class CachedTenantStore(EfTenantStore inner, IMemoryCache cache) : ITenantStore
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var key = $"tenant-by-slug:{slug.ToLowerInvariant()}";

        if (cache.TryGetValue(key, out Tenant? cached))
        {
            return cached;
        }

        var tenant = await inner.GetBySlugAsync(slug, ct);

        if (tenant is not null)
        {
            cache.Set(key, tenant, Expiration);
        }

        return tenant;
    }
}
