namespace SaasDemo.Domain.Tenants;

/// <summary>A client organisation, addressed in URLs by its <see cref="Slug"/>.</summary>
public sealed class Tenant
{
    public Tenant(Guid id, string slug, string name, Guid entraTenantId, bool isActive, DateTime createdAtUtc)
    {
        if (!TenantSlug.IsValid(slug) || TenantSlug.IsReserved(slug))
        {
            throw new ArgumentException($"'{slug}' is not a valid tenant slug.", nameof(slug));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Slug = slug;
        Name = name;
        EntraTenantId = entraTenantId;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; }
    public string Name { get; private set; }
    public Guid EntraTenantId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Links the tenant to a different Entra directory (used by the development seeder).</summary>
    public void LinkEntraTenant(Guid entraTenantId) => EntraTenantId = entraTenantId;
}
