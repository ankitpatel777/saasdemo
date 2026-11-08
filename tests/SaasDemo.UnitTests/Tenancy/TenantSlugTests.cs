using FluentAssertions;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.UnitTests.Tenancy;

public class TenantSlugTests
{
    [Theory]
    [InlineData("demo")]
    [InlineData("abc")]
    [InlineData("acme-corp-2")]
    [InlineData("a23456789012345678901234567890")] // 30 chars
    public void IsValid_AcceptsWellFormedSlugs(string slug) =>
        TenantSlug.IsValid(slug).Should().BeTrue();

    [Theory]
    [InlineData("Demo")]        // uppercase
    [InlineData("ab")]          // too short
    [InlineData("a234567890123456789012345678901")] // 31 chars
    [InlineData("bad_slug!")]   // invalid chars
    [InlineData("-demo")]       // leading dash
    [InlineData("demo-")]       // trailing dash
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_RejectsMalformedSlugs(string? slug) =>
        TenantSlug.IsValid(slug).Should().BeFalse();

    [Theory]
    [InlineData("health")]
    [InlineData("Platform")]
    public void IsReserved_FlagsPlatformSegments(string slug) =>
        TenantSlug.IsReserved(slug).Should().BeTrue();
}
