using FluentAssertions;
using Microsoft.Extensions.Options;
using SaasDemo.Infrastructure.Tenancy;

namespace SaasDemo.UnitTests.Tenancy;

public class InMemoryTenantStoreTests
{
    private static InMemoryTenantStore CreateStore() =>
        new(Options.Create(new TenantStoreOptions
        {
            Seed = [new TenantSeed { Slug = "demo", Name = "Demo Client", IsActive = true }],
        }), TimeProvider.System);

    [Theory]
    [InlineData("demo")]
    [InlineData("DEMO")]
    [InlineData("Demo")]
    public async Task GetBySlugAsync_IsCaseInsensitive(string slug)
    {
        var tenant = await CreateStore().GetBySlugAsync(slug, CancellationToken.None);

        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Demo Client");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNullForUnknownSlug() =>
        (await CreateStore().GetBySlugAsync("nope", CancellationToken.None)).Should().BeNull();
}
