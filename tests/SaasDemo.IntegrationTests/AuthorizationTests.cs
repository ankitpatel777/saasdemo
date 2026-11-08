using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using static SaasDemo.IntegrationTests.SaasDemoApiFactory;

namespace SaasDemo.IntegrationTests;

/// <summary>Authentication (401) and the ApiScope / TenantMember / role policies (403).</summary>
public class AuthorizationTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record UserDto(Guid Id, string Email, string DisplayName, string Role);
    private sealed record UserPage(List<UserDto> Items, int Page, int PageSize, int TotalCount);

    [Theory]
    [InlineData("/api/demo/info")]
    [InlineData("/api/demo/users")]
    [InlineData("/api/demo/me")]
    [InlineData("/api/unknown/info")]   // anonymous callers can't probe which tenants exist...
    [InlineData("/api/BAD_SLUG!/info")] // ...or which slugs are valid: always 401
    [InlineData("/api/platform/my-tenant")]
    public async Task NoToken_Returns401ProblemDetails(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task User_CanReadInfo_ButNotUsers()
    {
        var client = factory.CreateClientFor(Bob);

        (await client.GetAsync("/api/demo/info")).StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await client.GetAsync("/api/demo/users");
        users.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await users.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("Admin role");
    }

    [Fact]
    public async Task Admin_CanListUsers_OfOwnTenantOnly()
    {
        var demo = await factory.CreateClientFor(Alice).GetFromJsonAsync<UserPage>("/api/demo/users?pageSize=50");
        var other = await factory.CreateClientFor(Eve).GetFromJsonAsync<UserPage>("/api/other/users?pageSize=50");

        demo!.Items.Select(u => u.Email).Should().Contain(["alice@demo.example", "bob@demo.example"])
            .And.NotContain("eve@other.example");
        other!.Items.Select(u => u.Email).Should().Equal("eve@other.example");
    }

    [Theory]
    [InlineData("/api/demo/info")]
    [InlineData("/api/demo/users")]
    public async Task WrongTenant_Returns403(string path)
    {
        // A real admin, but of another organization: authenticated, yet not a member of "demo".
        var client = factory.CreateClientFor(TestUser.For(OtherEntraTenantId, "Admin"));

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("organization");
    }

    [Fact]
    public async Task MissingScope_Returns403()
    {
        var client = factory.CreateClientFor(TestUser.For(DemoEntraTenantId, "Admin") with { Scope = null });

        var response = await client.GetAsync("/api/demo/info");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("access_as_user");
    }

    [Fact]
    public async Task OtherScopeOnly_Returns403()
    {
        var client = factory.CreateClientFor(TestUser.For(DemoEntraTenantId, "Admin") with { Scope = "User.Read" });

        (await client.GetAsync("/api/platform/my-tenant")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NoAppRole_Returns403WithClearDetail()
    {
        var client = factory.CreateClientFor(TestUser.For(DemoEntraTenantId));

        var response = await client.GetAsync("/api/demo/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail
            .Should().Be("No role assigned in your organization.");
    }

    [Fact]
    public async Task Production_SameRulesApply()
    {
        var production = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

        (await production.CreateClient().GetAsync("/api/demo/info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await production.CreateClient().GetAsync("/api/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = production.CreateClientFor(Alice);
        (await admin.GetAsync("/api/demo/info")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/demo/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
