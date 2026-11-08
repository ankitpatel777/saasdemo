using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using SaasDemo.Infrastructure.Tenancy;
using SaasDemo.UnitTests.Persistence;

namespace SaasDemo.UnitTests.Tenancy;

public sealed class EfTenantStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Theory]
    [InlineData("tenant-a")]
    [InlineData("TENANT-A")]
    [InlineData("Tenant-A")]
    public async Task GetBySlugAsync_IsCaseInsensitive(string slug)
    {
        await using var db = _database.CreateContext(tenantId: null);

        var tenant = await new EfTenantStore(db).GetBySlugAsync(slug, CancellationToken.None);

        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Tenant A");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNullForUnknownSlug()
    {
        await using var db = _database.CreateContext(tenantId: null);

        (await new EfTenantStore(db).GetBySlugAsync("nope", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task CachedStore_ServesRepeatLookupsFromCache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await using var db = _database.CreateContext(tenantId: null);
        var store = new CachedTenantStore(new EfTenantStore(db), cache);

        var first = await store.GetBySlugAsync("tenant-a", CancellationToken.None);
        var second = await store.GetBySlugAsync("TENANT-A", CancellationToken.None);

        second.Should().BeSameAs(first);
    }
}
