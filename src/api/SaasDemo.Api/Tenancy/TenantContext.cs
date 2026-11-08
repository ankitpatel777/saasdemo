using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.Api.Tenancy;

/// <summary>
/// Per-request tenant state. Registered as scoped; only the resolution middleware sets it,
/// everyone else sees it through the read-only <see cref="ITenantContext"/>.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid EntraTenantId { get; private set; }
    public bool IsResolved { get; private set; }

    internal void Set(Tenant tenant)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("The tenant for this request has already been resolved.");
        }

        TenantId = tenant.Id;
        Slug = tenant.Slug;
        Name = tenant.Name;
        EntraTenantId = tenant.EntraTenantId;
        IsResolved = true;
    }
}
