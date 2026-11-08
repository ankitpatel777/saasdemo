using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SaasDemo.Domain.Audit;
using SaasDemo.Domain.Users;
using static SaasDemo.IntegrationTests.SaasDemoApiFactory;

namespace SaasDemo.IntegrationTests;

/// <summary>PUT /settings. Its own fixture: renaming "demo" must not leak into other test classes.</summary>
public class SettingsTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record SettingsDto(string Name);
    private sealed record TenantDto(string Slug, string Name);
    private sealed record MeDto(Guid Id, string Email, string DisplayName, string Role, TenantDto Tenant);

    [Fact]
    public async Task Rename_UpdatesTheTenant_EvictsTheCache_AndIsAudited()
    {
        var admin = factory.CreateClientFor(Alice);
        (await admin.GetAsync("/api/demo/me")).EnsureSuccessStatusCode(); // caches the tenant (and its name)

        var response = await admin.PutAsJsonAsync("/api/demo/settings", new { name = "  Demo Holdings  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SettingsDto>())!.Name.Should().Be("Demo Holdings");
        factory.GetTenant(DemoTenantId).Name.Should().Be("Demo Holdings");
        factory.GetTenant(DemoTenantId).Slug.Should().Be("demo");

        // Next request sees the new name: the 5-minute tenant cache was evicted.
        (await admin.GetFromJsonAsync<MeDto>("/api/demo/me"))!.Tenant.Name.Should().Be("Demo Holdings");

        factory.GetAuditEvents(DemoTenantId).Should().Contain(e =>
            e.Action == AuditAction.TenantSettingsUpdated && e.ActorDisplayName == "Alice" && e.Details!.Contains("Demo Holdings"));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Rename_InvalidName_Returns400(string? name)
    {
        var response = await factory.CreateClientFor(Alice).PutAsJsonAsync("/api/demo/settings", new { name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rename_TooLong_Returns400()
    {
        var response = await factory.CreateClientFor(Alice).PutAsJsonAsync("/api/demo/settings", new { name = new string('a', 101) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rename_RequiresAdmin()
    {
        var response = await factory.CreateClientFor(Bob).PutAsJsonAsync("/api/demo/settings", new { name = "Hijacked" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

/// <summary>GET /dashboard/summary. Its own fixture, so the expected numbers are exact.</summary>
public class SummaryTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record SummaryDto(int TotalUsers, int ActiveUsers, int Admins, int SignInsLast7Days);

    [Fact]
    public async Task Summary_CountsThisTenantOnly()
    {
        // Seed: Alice (Admin), Bob (User). Add an inactive admin and an active user.
        factory.AddUser(DemoTenantId, "old.admin@demo.example", "Old Admin", UserRole.Admin, isActive: false);
        factory.AddUser(DemoTenantId, "carl@demo.example", "Carl", UserRole.User);
        var admin = factory.CreateClientFor(Alice);
        (await admin.GetAsync("/api/demo/me")).EnsureSuccessStatusCode();                          // sign-in 1
        (await factory.CreateClientFor(Bob).GetAsync("/api/demo/me")).EnsureSuccessStatusCode();   // sign-in 2
        (await factory.CreateClientFor(Eve).GetAsync("/api/other/me")).EnsureSuccessStatusCode();  // other tenant

        var summary = await admin.GetFromJsonAsync<SummaryDto>("/api/demo/dashboard/summary");

        summary.Should().Be(new SummaryDto(TotalUsers: 4, ActiveUsers: 3, Admins: 2, SignInsLast7Days: 2));
    }
}
