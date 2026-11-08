namespace SaasDemo.Application.Tenancy;

/// <summary>
/// The tenant for the current request, resolved from the URL path. Read-only for consumers.
/// Only trust it when <see cref="IsResolved"/> is true.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string Slug { get; }
    string Name { get; }
    Guid EntraTenantId { get; }
    bool IsResolved { get; }
}
