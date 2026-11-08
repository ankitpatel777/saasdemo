using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SaasDemo.Api.Tenancy;

namespace SaasDemo.UnitTests;

public class TenantResolutionMiddlewareTests
{
    private static readonly Guid ConfiguredTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task InvokeAsync_AllowsAuthenticatedRequestForMatchingTenant()
    {
        var nextCalled = false;
        var context = CreateContext(ConfiguredTenantId);
        var tenantContext = new TenantContext();
        var middleware = CreateMiddleware(() => nextCalled = true);

        await middleware.InvokeAsync(context, tenantContext);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        tenantContext.EntraTenantId.Should().Be(ConfiguredTenantId);
    }

    [Fact]
    public async Task InvokeAsync_RejectsAuthenticatedRequestForDifferentTenant()
    {
        var nextCalled = false;
        var context = CreateContext(OtherTenantId);
        var middleware = CreateMiddleware(() => nextCalled = true);

        await middleware.InvokeAsync(context, new TenantContext());

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_RejectsAuthenticatedRequestWithoutTidClaim()
    {
        var nextCalled = false;
        var context = CreateContext(null);
        var middleware = CreateMiddleware(() => nextCalled = true);

        await middleware.InvokeAsync(context, new TenantContext());

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotCompareTenantForUnauthenticatedRequest()
    {
        var nextCalled = false;
        var context = CreateContext(OtherTenantId);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        var middleware = CreateMiddleware(() => nextCalled = true);

        await middleware.InvokeAsync(context, new TenantContext());

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsNotFoundForUnconfiguredTenant()
    {
        var nextCalled = false;
        var context = CreateContext(ConfiguredTenantId);
        context.Request.Path = "/api/unknown/widgets";
        var middleware = CreateMiddleware(() => nextCalled = true);

        await middleware.InvokeAsync(context, new TenantContext());

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotResolveHealthRouteAsTenant()
    {
        var nextCalled = false;
        var context = CreateContext(ConfiguredTenantId);
        context.Request.Path = "/api/health";
        var tenantContext = new TenantContext();
        var middleware = CreateMiddleware(() => nextCalled = true);

        await middleware.InvokeAsync(context, tenantContext);

        nextCalled.Should().BeTrue();
        tenantContext.EntraTenantId.Should().BeNull();
    }

    private static TenantResolutionMiddleware CreateMiddleware(Action onNext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:acme"] = ConfiguredTenantId.ToString()
            })
            .Build();

        return new TenantResolutionMiddleware(_ =>
        {
            onNext();
            return Task.CompletedTask;
        }, configuration);
    }

    private static DefaultHttpContext CreateContext(Guid? tenantId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/acme/widgets";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            tenantId is null ? [] : [new Claim("tid", tenantId.Value.ToString())],
            authenticationType: "Bearer"));
        return context;
    }
}
