using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SaasDemo.Domain.Users;
using static SaasDemo.IntegrationTests.SaasDemoApiFactory;

namespace SaasDemo.IntegrationTests;

/// <summary>ActiveTenantUser: every TenantUser/TenantAdmin endpoint needs a provisioned, active user (part of #9).</summary>
public class ActiveUserTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record SignInDto(DateTime OccurredAtUtc);

    [Fact]
    public async Task NotProvisionedUser_Gets403_ButMayCallMe()
    {
        var client = factory.CreateClientFor(TestUser.For(DemoEntraTenantId, "User"));

        var info = await client.GetAsync("/api/demo/info");
        info.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await info.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("not set up yet");

        (await client.GetAsync("/api/demo/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/demo/info")).StatusCode.Should().Be(HttpStatusCode.OK); // misses are not cached
    }

    [Theory]
    [InlineData("User", "/api/demo/info")]
    [InlineData("User", "/api/demo/me/activity")]
    [InlineData("Admin", "/api/demo/users")]
    [InlineData("Admin", "/api/demo/dashboard/summary")]
    public async Task DeactivationAppliesImmediately_AndReactivationRestoresAccess(string role, string path)
    {
        var target = factory.AddUser(DemoTenantId, $"{Guid.NewGuid():N}@demo.example", "Target", Enum.Parse<UserRole>(role));
        var targetClient = factory.CreateClientFor(new TestUser(DemoEntraTenantId, target.EntraObjectId, [role]));
        var admin = factory.CreateClientFor(Alice);

        // Warm the 30-second cache with "active", then deactivate: eviction must make the change immediate.
        (await targetClient.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/demo/users/{target.Id}/deactivate", null)).EnsureSuccessStatusCode();

        var denied = await targetClient.GetAsync(path);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("deactivated");

        (await admin.PostAsync($"/api/demo/users/{target.Id}/reactivate", null)).EnsureSuccessStatusCode();
        (await targetClient.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_AuditsProvisioningOnce_AndSignInsAtMostEvery30Minutes()
    {
        var user = new TestUser(DemoEntraTenantId, Guid.NewGuid(), ["User"], Name: "Dana", Email: "dana@demo.example");
        var client = factory.CreateClientFor(user);

        (await client.GetAsync("/api/demo/me")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/demo/me")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/demo/me")).EnsureSuccessStatusCode();

        var stored = factory.GetUsers(DemoTenantId).Single(u => u.EntraObjectId == user.ObjectId);
        var events = factory.GetAuditEvents(DemoTenantId).Where(e => e.TargetUserId == stored.Id).ToList();
        events.Select(e => e.Action.ToString()).Should().BeEquivalentTo(["UserProvisioned", "UserSignedIn"]);
        events.Should().OnlyContain(e => e.ActorUserId == stored.Id && e.ActorDisplayName == "Dana");

        var activity = await client.GetFromJsonAsync<List<SignInDto>>("/api/demo/me/activity");
        activity.Should().ContainSingle();
        activity![0].OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }
}
