using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.Infrastructure.Tenancy;

/// <summary>
/// Tenants seeded from configuration ("Tenants:Seed"). Stand-in until the SQL-backed store exists.
/// Immutable after construction, so it is safe as a singleton.
/// </summary>
public sealed class InMemoryTenantStore : ITenantStore
{
    private readonly IReadOnlyDictionary<string, Tenant> _tenants;

    public InMemoryTenantStore(IOptions<TenantStoreOptions> options, TimeProvider timeProvider)
    {
        var createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var tenants = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in options.Value.Seed)
        {
            var slug = seed.Slug.ToLowerInvariant();
            var tenant = new Tenant(StableId(slug), slug, seed.Name, seed.EntraTenantId, seed.IsActive, createdAtUtc);

            if (!tenants.TryAdd(slug, tenant))
            {
                throw new InvalidOperationException($"Duplicate tenant slug '{slug}' in configuration.");
            }
        }

        _tenants = tenants;
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_tenants.GetValueOrDefault(slug));
    }

    // Same slug -> same Id across restarts, so Ids seen by clients stay stable.
    private static Guid StableId(string slug) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"tenant:{slug}")).AsSpan(0, 16));
}
