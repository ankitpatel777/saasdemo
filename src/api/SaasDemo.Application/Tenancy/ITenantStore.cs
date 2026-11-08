using SaasDemo.Domain.Tenants;

namespace SaasDemo.Application.Tenancy;

public interface ITenantStore
{
    /// <summary>Finds a tenant by slug (case-insensitive). Returns null when none exists.</summary>
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct);
}
