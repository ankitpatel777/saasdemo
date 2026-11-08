namespace SaasDemo.Domain.Common;

/// <summary>
/// Marks data that belongs to exactly one tenant. Every such entity is isolated twice:
/// by an EF Core global query filter and by SQL Server Row-Level Security.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
