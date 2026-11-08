namespace SaasDemo.Api.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? EntraTenantId { get; private set; }

    internal void Resolve(Guid entraTenantId) => EntraTenantId = entraTenantId;
}
