using SaasDemo.Domain.Tenants;

namespace SaasDemo.Application.Tenancy;

public enum TenantResolutionStatus
{
    Resolved,
    InvalidSlug,
    NotFound,
}

public sealed record TenantResolution(TenantResolutionStatus Status, Tenant? Tenant = null);

/// <summary>Turns a slug from the URL into an active tenant, or a reason why it can't.</summary>
public sealed class TenantResolver(ITenantStore store)
{
    public async Task<TenantResolution> ResolveAsync(string slug, CancellationToken ct)
    {
        if (!TenantSlug.IsValid(slug))
        {
            return new(TenantResolutionStatus.InvalidSlug);
        }

        var tenant = await store.GetBySlugAsync(slug, ct);

        // Unknown and inactive look identical to callers so tenant existence can't be probed.
        return tenant is { IsActive: true }
            ? new(TenantResolutionStatus.Resolved, tenant)
            : new(TenantResolutionStatus.NotFound);
    }
}
