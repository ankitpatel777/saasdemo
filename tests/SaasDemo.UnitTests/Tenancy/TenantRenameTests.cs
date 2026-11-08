using FluentAssertions;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.UnitTests.Tenancy;

public class TenantRenameTests
{
    private static Tenant NewTenant() => new(Guid.NewGuid(), "demo", "Demo Client", Guid.NewGuid(), true, DateTime.UtcNow);

    [Theory]
    [InlineData("AB", "AB")]
    [InlineData("  Contoso Ltd  ", "Contoso Ltd")]
    public void Rename_TrimsAndStoresValidNames(string input, string expected)
    {
        var tenant = NewTenant();

        tenant.Rename(input);

        tenant.Name.Should().Be(expected);
        tenant.Slug.Should().Be("demo");
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   A   ")]
    public void Rename_RejectsTooShortNames(string input) =>
        FluentActions.Invoking(() => NewTenant().Rename(input)).Should().Throw<ArgumentException>();

    [Fact]
    public void Rename_RejectsNamesOver100Characters()
    {
        var tenant = NewTenant();

        FluentActions.Invoking(() => tenant.Rename(new string('a', 101))).Should().Throw<ArgumentException>();
        tenant.Rename(new string('a', 100));
        tenant.Name.Should().HaveLength(100);
    }
}
