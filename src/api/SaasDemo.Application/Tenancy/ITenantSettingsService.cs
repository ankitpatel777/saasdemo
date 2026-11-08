using SaasDemo.Application.Audit;

namespace SaasDemo.Application.Tenancy;

public interface ITenantSettingsService
{
    /// <summary>Renames the current tenant, refreshes cached lookups and audits it. Returns the stored name.</summary>
    /// <exception cref="ArgumentException">The name is not valid (see Tenant.Rename).</exception>
    Task<string> RenameAsync(string name, AuditActor actor, CancellationToken ct);
}
