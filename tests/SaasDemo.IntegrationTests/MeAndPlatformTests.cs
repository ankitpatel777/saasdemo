using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SaasDemo.Domain.Users;
using static SaasDemo.IntegrationTests.SaasDemoApiFactory;

namespace SaasDemo.IntegrationTests;

/// <summary>Just-in-time provisioning (/me) and the tenant-less /api/platform/my-tenant.</summary>
public class MeAndPlatformTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record TenantDto(string Slug, string Name);
    private sealed record MeDto(Guid Id, string Email, string DisplayName, string Role, TenantDto Tenant);

    [Fact]
    public async Task Me_CreatesTheUserOnce()
    {
        var user = new TestUser(DemoEntraTenantId, Guid.NewGuid(), ["User"], Name: "Carol", Email: "carol@demo.example");
        var client = factory.CreateClientFor(user);

        var first = await client.GetFromJsonAsync<MeDto>("/api/demo/me");
        var second = await client.GetFromJsonAsync<MeDto>("/api/demo/me");

        first!.Email.Should().Be("carol@demo.example");
        first.DisplayName.Should().Be("Carol");
        first.Role.Should().Be("User");
        first.Tenant.Should().Be(new TenantDto("demo", "Demo Client"));
        second!.Id.Should().Be(first.Id);

        var stored = factory.GetUsers(DemoTenantId).Where(u => u.EntraObjectId == user.ObjectId).ToList();
        stored.Should().ContainSingle();
        stored[0].TenantId.Should().Be(DemoTenantId);
        stored[0].LastLoginAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Me_SyncsRoleFromTokenOnEveryCall()
    {
        var oid = Guid.NewGuid();

        var asAdmin = await factory.CreateClientFor(new TestUser(DemoEntraTenantId, oid, ["Admin"]))
            .GetFromJsonAsync<MeDto>("/api/demo/me");
        var demoted = await factory.CreateClientFor(new TestUser(DemoEntraTenantId, oid, ["User"]))
            .GetFromJsonAsync<MeDto>("/api/demo/me");

        asAdmin!.Role.Should().Be("Admin");
        demoted!.Role.Should().Be("User");
        factory.GetUsers(DemoTenantId).Single(u => u.EntraObjectId == oid).Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task Me_DeactivatedUser_Gets403AndNothingIsUpdated()
    {
        var oid = Guid.NewGuid();
        (await factory.CreateClientFor(new TestUser(DemoEntraTenantId, oid, ["User"])).GetAsync("/api/demo/me"))
            .EnsureSuccessStatusCode();
        factory.DeactivateUser(DemoTenantId, oid);
        var before = factory.GetUsers(DemoTenantId).Single(u => u.EntraObjectId == oid);

        // Even with a now-elevated role in the token, a deactivated user is refused and not touched.
        var response = await factory.CreateClientFor(new TestUser(DemoEntraTenantId, oid, ["Admin"]))
            .GetAsync("/api/demo/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail
            .Should().Be("Your account has been deactivated by your organization's administrator.");

        var after = factory.GetUsers(DemoTenantId).Single(u => u.EntraObjectId == oid);
        after.LastLoginAtUtc.Should().Be(before.LastLoginAtUtc);
        after.Role.Should().Be(UserRole.User);
        after.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Me_AdminWinsWhenBothRolesAreAssigned()
    {
        var me = await factory.CreateClientFor(TestUser.For(DemoEntraTenantId, "User", "Admin"))
            .GetFromJsonAsync<MeDto>("/api/demo/me");

        me!.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task MyTenant_ReturnsSlugForKnownTid()
    {
        var client = factory.CreateClientFor(TestUser.For(DemoEntraTenantId, "User"));

        var tenant = await client.GetFromJsonAsync<TenantDto>("/api/platform/my-tenant");

        tenant.Should().Be(new TenantDto("demo", "Demo Client"));
    }

    [Theory]
    [InlineData(false)] // a directory we have never seen
    [InlineData(true)]  // a registered but inactive tenant looks the same
    public async Task MyTenant_Returns404ForUnregisteredOrganization(bool inactive)
    {
        var tid = inactive ? PausedEntraTenantId : Guid.NewGuid();
        var client = factory.CreateClientFor(TestUser.For(tid, "Admin"));

        var response = await client.GetAsync("/api/platform/my-tenant");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title.Should().Be("Organization not registered");
    }
}
