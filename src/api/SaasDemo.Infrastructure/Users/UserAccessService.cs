using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SaasDemo.Application.Tenancy;
using SaasDemo.Application.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Users;

/// <summary>
/// Caches "is this person an active user here?" for 30 seconds per (tenant, oid), because every protected
/// request asks it. Deactivate/reactivate evict the entry, so they apply immediately on this instance.
/// With several instances, another instance may serve a stale answer for up to 30 seconds (the bound).
/// Unknown users are not cached, so a first sign-in works immediately.
/// </summary>
public sealed class UserAccessService(AppDbContext db, ITenantContext tenantContext, IMemoryCache cache)
    : IUserAccessService
{
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(30);

    public async Task<UserAccess?> GetAsync(Guid entraObjectId, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return null;
        }

        var key = Key(tenantContext.TenantId, entraObjectId);
        if (cache.TryGetValue(key, out UserAccess? cached))
        {
            return cached;
        }

        // The query filter (and RLS) scope this to the current tenant.
        var access = await db.Users
            .AsNoTracking()
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => new UserAccess(u.Id, u.DisplayName, u.IsActive))
            .SingleOrDefaultAsync(ct);

        if (access is not null)
        {
            cache.Set(key, access, Expiration);
        }

        return access;
    }

    public void Evict(Guid tenantId, Guid entraObjectId) => cache.Remove(Key(tenantId, entraObjectId));

    private static string Key(Guid tenantId, Guid entraObjectId) => $"user-access:{tenantId}:{entraObjectId}";
}
