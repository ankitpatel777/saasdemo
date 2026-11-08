using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Audit;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Audit;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Tenancy;

public sealed class TenantSettingsService(
    AppDbContext db,
    ITenantContext tenantContext,
    IAuditLog auditLog,
    CachedTenantStore tenantCache) : ITenantSettingsService
{
    public async Task<string> RenameAsync(string name, AuditActor actor, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("No tenant is resolved for this request.");
        }

        // Tenants are not tenant-owned (no query filter, no RLS), so pick the current tenant explicitly by
        // its resolved id: never by anything taken from the request.
        var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantContext.TenantId, ct);
        var previous = tenant.Name;

        tenant.Rename(name);
        if (tenant.Name == previous)
        {
            return tenant.Name;
        }

        auditLog.Record(actor, AuditAction.TenantSettingsUpdated, details: $"Renamed from \"{previous}\" to \"{tenant.Name}\"");
        await db.SaveChangesAsync(ct);

        tenantCache.Evict(tenant);
        return tenant.Name;
    }
}
