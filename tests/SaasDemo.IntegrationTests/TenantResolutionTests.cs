using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using static SaasDemo.IntegrationTests.SaasDemoApiFactory;

namespace SaasDemo.IntegrationTests;

/// <summary>Tenant resolution for authenticated callers, plus the anonymous health endpoints.</summary>
public class TenantResolutionTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record TenantInfo(Guid TenantId, string Slug, string Name);

    private readonly HttpClient _demoUser = factory.CreateClientFor(TestUser.For(DemoEntraTenantId, "User"));

    [Fact]
    public async Task KnownTenant_ReturnsInfoFromTheDatabase()
    {
        var info = await _demoUser.GetFromJsonAsync<TenantInfo>("/api/demo/info");

        info!.TenantId.Should().Be(DemoTenantId);
        info.Slug.Should().Be("demo");
        info.Name.Should().Be("Demo Client");
    }

    [Fact]
    public async Task Info_DoesNotExposeEntraTenantId()
    {
        var json = await _demoUser.GetStringAsync("/api/demo/info");

        json.Should().NotContainEquivalentOf("entraTenantId");
    }

    [Theory]
    [InlineData("/api/unknown/info")] // does not exist
    [InlineData("/api/paused/info")]  // exists but inactive: same 404
    public async Task UnknownOrInactiveTenant_Returns404ProblemDetails(string path)
    {
        var response = await _demoUser.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task MalformedSlug_Returns400ProblemDetails()
    {
        var response = await _demoUser.GetAsync("/api/BAD_SLUG!/info");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task HealthEndpoints_StayAnonymous()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        var ready = await anonymous.GetAsync("/api/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
