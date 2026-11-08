using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SaasDemo.IntegrationTests;

/// <summary>Runs the API as Production: tenant routes are closed to anonymous callers.</summary>
public class ProductionTenantRoutesTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
        .CreateClient();

    [Theory]
    [InlineData("/api/demo/info")]     // known tenant: info endpoint is not exposed
    [InlineData("/api/unknown/info")]  // unknown tenant: same answer, no enumeration
    [InlineData("/api/BAD_SLUG!/info")] // malformed slug: same answer, nothing leaks
    [InlineData("/api/demo/anything")]
    public async Task AnonymousTenantRoute_Returns401ProblemDetails(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Health_WorksWithoutTenantOrAuth()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
