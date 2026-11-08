using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SaasDemo.IntegrationTests;

public class TenantResolutionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record TenantInfo(Guid TenantId, string Slug, string Name);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task KnownTenant_Returns200WithTenantInfo()
    {
        var response = await _client.GetAsync("/api/demo/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TenantInfo>();
        body!.Slug.Should().Be("demo");
        body.Name.Should().Be("Demo Client");
        body.TenantId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UnknownTenant_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/unknown/info");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task MalformedSlug_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/BAD_SLUG!/info");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Health_WorksWithoutTenant()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
