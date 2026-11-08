using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SaasDemo.Domain.Audit;
using SaasDemo.Domain.Users;
using static SaasDemo.IntegrationTests.SaasDemoApiFactory;

namespace SaasDemo.IntegrationTests;

/// <summary>GET /users (server-side paging, filtering, sorting), deactivate/reactivate and the audit log.</summary>
public class UserManagementTests(SaasDemoApiFactory factory) : IClassFixture<SaasDemoApiFactory>
{
    private sealed record UserDto(
        Guid Id, string Email, string DisplayName, string Role, bool IsActive, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);

    private sealed record PageDto<T>(List<T> Items, int Page, int PageSize, int TotalCount);

    private sealed record AuditDto(
        Guid Id, DateTime OccurredAtUtc, string Action, Guid? ActorUserId, string ActorDisplayName,
        Guid? TargetUserId, string? TargetDisplayName, string? Details);

    private readonly HttpClient _admin = factory.CreateClientFor(Alice);

    /// <summary>Five users whose names share a unique prefix, so other tests' users never match the search.</summary>
    private (string Prefix, List<AppUser> Users) SeedFive()
    {
        var prefix = $"p{Guid.NewGuid():N}"[..10];
        var day = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var users = new List<AppUser>
        {
            factory.AddUser(DemoTenantId, $"{prefix}-e@demo.example", $"{prefix} Anna", UserRole.Admin, createdAtUtc: day.AddDays(5), lastLoginAtUtc: day.AddDays(9)),
            factory.AddUser(DemoTenantId, $"{prefix}-d@demo.example", $"{prefix} Ben", UserRole.User, createdAtUtc: day.AddDays(4), lastLoginAtUtc: day.AddDays(10)),
            factory.AddUser(DemoTenantId, $"{prefix}-c@demo.example", $"{prefix} Cleo", UserRole.User, isActive: false, createdAtUtc: day.AddDays(3)),
            factory.AddUser(DemoTenantId, $"{prefix}-b@demo.example", $"{prefix} Dev", UserRole.Admin, createdAtUtc: day.AddDays(2), lastLoginAtUtc: day.AddDays(7)),
            factory.AddUser(DemoTenantId, $"{prefix}-a@demo.example", $"{prefix} Eli", UserRole.User, createdAtUtc: day.AddDays(1), lastLoginAtUtc: day.AddDays(8)),
        };
        return (prefix, users);
    }

    private async Task<PageDto<UserDto>> ListAsync(string query) =>
        (await _admin.GetFromJsonAsync<PageDto<UserDto>>($"/api/demo/users?{query}"))!;

    private static IEnumerable<string> Names(PageDto<UserDto> page, string prefix) =>
        page.Items.Select(u => u.DisplayName[(prefix.Length + 1)..]);

    [Fact]
    public async Task Users_PagesAndSortsOnTheServer()
    {
        var (prefix, _) = SeedFive();

        var page2 = await ListAsync($"search={prefix}&sort=displayName&dir=asc&page=2&pageSize=2");
        page2.TotalCount.Should().Be(5);
        page2.Page.Should().Be(2);
        page2.PageSize.Should().Be(2);
        Names(page2, prefix).Should().Equal("Cleo", "Dev");

        Names(await ListAsync($"search={prefix}&sort=displayName&dir=desc&page=3&pageSize=2"), prefix).Should().Equal("Anna");
        Names(await ListAsync($"search={prefix}&sort=email&dir=asc"), prefix).Should().Equal("Eli", "Dev", "Cleo", "Ben", "Anna");
        Names(await ListAsync($"search={prefix}&sort=createdAt&dir=desc"), prefix).Should().Equal("Anna", "Ben", "Cleo", "Dev", "Eli");

        // SQL Server and SQLite both sort NULL first ascending, so Cleo (never signed in) leads.
        Names(await ListAsync($"search={prefix}&sort=lastLogin&dir=asc"), prefix).Should().Equal("Cleo", "Dev", "Eli", "Anna", "Ben");
    }

    [Fact]
    public async Task Users_FiltersByRoleStatusAndSearch()
    {
        var (prefix, _) = SeedFive();

        Names(await ListAsync($"search={prefix}&role=Admin"), prefix).Should().Equal("Anna", "Dev");
        Names(await ListAsync($"search={prefix}&status=inactive"), prefix).Should().Equal("Cleo");
        (await ListAsync($"search={prefix}&status=active&role=user")).TotalCount.Should().Be(2);

        // Case-insensitive, and matches the email as well as the name.
        Names(await ListAsync($"search={prefix.ToUpperInvariant()}-B@"), prefix).Should().Equal("Dev");
        Names(await ListAsync($"search={prefix}%20cle"), prefix).Should().Equal("Cleo");

        // LIKE wildcards in the search box match literally.
        (await ListAsync($"search={prefix}%25")).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Users_ReturnsAllFieldsOfTheItem()
    {
        var (prefix, users) = SeedFive();

        var anna = (await ListAsync($"search={prefix}-e@")).Items.Single();

        anna.Id.Should().Be(users[0].Id);
        anna.Email.Should().Be($"{prefix}-e@demo.example");
        anna.Role.Should().Be("Admin");
        anna.IsActive.Should().BeTrue();
        anna.CreatedAtUtc.Should().Be(new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc));
        anna.LastLoginAtUtc.Should().Be(new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("pageSize=51")]
    [InlineData("pageSize=0")]
    [InlineData("page=0")]
    [InlineData("sort=password")]
    [InlineData("dir=up")]
    [InlineData("role=Owner")]
    [InlineData("status=deleted")]
    public async Task Users_InvalidQuery_Returns400ValidationProblem(string query)
    {
        var response = await _admin.GetAsync($"/api/demo/users?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Deactivate_IsIdempotent_AndAuditedOnce()
    {
        var target = factory.AddUser(DemoTenantId, $"{Guid.NewGuid():N}@demo.example", "Frank", UserRole.User);

        var first = await _admin.PostAsync($"/api/demo/users/{target.Id}/deactivate", null);
        var second = await _admin.PostAsync($"/api/demo/users/{target.Id}/deactivate", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<UserDto>())!.IsActive.Should().BeFalse();

        var events = factory.GetAuditEvents(DemoTenantId).Where(e => e.TargetUserId == target.Id).ToList();
        events.Should().ContainSingle(e => e.Action == AuditAction.UserDeactivated);
        events[0].ActorDisplayName.Should().Be("Alice");
        events[0].TenantId.Should().Be(DemoTenantId);

        var reactivated = await _admin.PostAsync($"/api/demo/users/{target.Id}/reactivate", null);
        (await reactivated.Content.ReadFromJsonAsync<UserDto>())!.IsActive.Should().BeTrue();
        factory.GetAuditEvents(DemoTenantId).Count(e => e.TargetUserId == target.Id).Should().Be(2);
    }

    [Fact]
    public async Task Deactivate_Yourself_Returns400()
    {
        var alice = factory.GetUsers(DemoTenantId).Single(u => u.EntraObjectId == AliceObjectId);

        var response = await _admin.PostAsync($"/api/demo/users/{alice.Id}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("You cannot deactivate yourself");
        factory.GetUsers(DemoTenantId).Single(u => u.Id == alice.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_UserOfAnotherTenant_Returns404AndChangesNothing()
    {
        var eve = factory.GetUsers(OtherTenantId).Single(u => u.EntraObjectId == EveObjectId);

        (await _admin.PostAsync($"/api/demo/users/{eve.Id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _admin.PostAsync($"/api/demo/users/{Guid.NewGuid()}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        factory.GetUsers(OtherTenantId).Single(u => u.Id == eve.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UserRole_CannotManageUsers()
    {
        var bobClient = factory.CreateClientFor(Bob);

        (await bobClient.GetAsync("/api/demo/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await bobClient.PostAsync($"/api/demo/users/{Guid.NewGuid()}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await bobClient.GetAsync("/api/demo/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Audit_ListsNewestFirst_WithReadableNames_AndOnlyThisTenant()
    {
        var target = factory.AddUser(DemoTenantId, $"{Guid.NewGuid():N}@demo.example", "Gina", UserRole.User);
        (await _admin.PostAsync($"/api/demo/users/{target.Id}/deactivate", null)).EnsureSuccessStatusCode();
        (await _admin.PostAsync($"/api/demo/users/{target.Id}/reactivate", null)).EnsureSuccessStatusCode();
        (await factory.CreateClientFor(Eve).GetAsync("/api/other/me")).EnsureSuccessStatusCode(); // an event in "other"

        var page = (await _admin.GetFromJsonAsync<PageDto<AuditDto>>("/api/demo/audit?pageSize=50"))!;

        page.Items.Should().BeInDescendingOrder(e => e.OccurredAtUtc);
        page.Items.Should().NotContain(e => e.ActorDisplayName == "Eve");
        var gina = page.Items.Where(e => e.TargetUserId == target.Id).ToList();
        gina.Select(e => e.Action).Should().Equal("UserReactivated", "UserDeactivated");
        gina.Should().OnlyContain(e => e.TargetDisplayName == "Gina" && e.ActorDisplayName == "Alice");

        var small = (await _admin.GetFromJsonAsync<PageDto<AuditDto>>("/api/demo/audit?page=1&pageSize=1"))!;
        small.Items.Should().ContainSingle();
        small.TotalCount.Should().Be(page.TotalCount);

        (await _admin.GetAsync("/api/demo/audit?pageSize=500")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
