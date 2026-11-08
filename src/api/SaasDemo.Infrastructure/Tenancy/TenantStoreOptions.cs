namespace SaasDemo.Infrastructure.Tenancy;

/// <summary>Bound from the "Tenants" configuration section.</summary>
public sealed class TenantStoreOptions
{
    public const string SectionName = "Tenants";

    public List<TenantSeed> Seed { get; init; } = [];
}

public sealed class TenantSeed
{
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid EntraTenantId { get; init; }

    // Safe default: a seed must opt in to being active.
    public bool IsActive { get; init; }
}
