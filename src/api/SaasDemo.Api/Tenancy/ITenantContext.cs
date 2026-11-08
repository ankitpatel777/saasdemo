namespace SaasDemo.Api.Tenancy;

public interface ITenantContext
{
    Guid? EntraTenantId { get; }
}
