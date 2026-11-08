using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using SaasDemo.Api.Authorization;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.UnitTests.Authorization;

public class TenantMemberHandlerTests
{
    private static readonly Guid EntraTenantId = Guid.NewGuid();

    private sealed class StubTenantContext(bool isResolved) : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public string Slug => "demo";
        public string Name => "Demo Client";
        public Guid EntraTenantId => TenantMemberHandlerTests.EntraTenantId;
        public bool IsResolved => isResolved;
    }

    private static async Task<AuthorizationHandlerContext> EvaluateAsync(bool tenantResolved, string? tid)
    {
        var claims = tid is null ? [] : new[] { new Claim("tid", tid) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new TenantMemberRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        var handler = new TenantMemberHandler(new StubTenantContext(tenantResolved), NullLogger<TenantMemberHandler>.Instance);
        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task Succeeds_WhenTokenTidMatchesTheResolvedTenant()
    {
        var context = await EvaluateAsync(tenantResolved: true, EntraTenantId.ToString());

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_WhenTokenTidBelongsToAnotherOrganization()
    {
        var context = await EvaluateAsync(tenantResolved: true, Guid.NewGuid().ToString());

        context.HasFailed.Should().BeTrue();
        context.FailureReasons.Should().ContainSingle().Which.Message.Should().Contain("organization");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task Fails_WhenTokenTidIsMissingOrMalformed(string? tid)
    {
        var context = await EvaluateAsync(tenantResolved: true, tid);

        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Fails_WhenNoTenantIsResolved()
    {
        var context = await EvaluateAsync(tenantResolved: false, EntraTenantId.ToString());

        context.HasFailed.Should().BeTrue();
    }
}
