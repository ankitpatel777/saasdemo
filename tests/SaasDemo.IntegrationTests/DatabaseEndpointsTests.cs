using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace SaasDemo.IntegrationTests;

/// <summary>Database-backed behaviour (SQLite). Development unless stated otherwise.</summary>
public class DatabaseEndpointsTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record UserDto(Guid Id, string Email, string DisplayName, string Role);
    private sealed record TenantInfo(Guid TenantId, string Slug, string Name);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Users_ReturnsOnlyTheRequestedTenantsUsers()
    {
        var demo = await _client.GetFromJsonAsync<List<UserDto>>("/api/demo/users") ?? [];
        var other = await _client.GetFromJsonAsync<List<UserDto>>("/api/other/users") ?? [];

        demo.Select(u => u.Email).Should().Equal("alice@demo.example", "bob@demo.example");
        demo.Single(u => u.Email == "alice@demo.example").Role.Should().Be("Admin");
        other.Select(u => u.Email).Should().Equal("eve@other.example");
    }

    [Fact]
    public async Task Info_ComesFromTheDatabase()
    {
        var info = await _client.GetFromJsonAsync<TenantInfo>("/api/demo/info");

        info!.TenantId.Should().Be(SaasDemoApiFactory.DemoTenantId);
        info.Name.Should().Be("Demo Client");
    }

    [Theory]
    [InlineData("/api/paused/info")]  // exists but inactive
    [InlineData("/api/paused/users")]
    [InlineData("/api/nobody/users")] // does not exist: same 404
    public async Task InactiveOrUnknownTenant_Returns404(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ready_ChecksTheDatabase()
    {
        var response = await _client.GetAsync("/api/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Production_UsersIsClosedAndReadyStillWorks()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        (await client.GetAsync("/api/demo/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
