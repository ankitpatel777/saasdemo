using System.Security.Claims;
using SaasDemo.Api.Authorization;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.Api.Endpoints;

public sealed record MyTenantResponse(string Slug, string Name);

/// <summary>
/// Endpoints under /api/platform: not tenant-scoped ("platform" is a reserved slug, so tenant
/// resolution skips these paths).
/// </summary>
public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/platform/my-tenant", GetMyTenant)
            .RequireAuthorization(Policies.ApiScope)
            .WithName("GetMyTenant")
            .Produces<MyTenantResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>Which client does the caller's organization (token tid) use? Lets the SPA route after sign-in.</summary>
    private static async Task<IResult> GetMyTenant(ClaimsPrincipal principal, ITenantStore tenants, CancellationToken ct)
    {
        var tenant = principal.GetEntraTenantId() is { } tid
            ? await tenants.GetByEntraTenantIdAsync(tid, ct)
            : null;

        // Inactive tenants look unregistered, same as tenant resolution (no existence leak).
        // Onboarding of new organizations arrives in step 7.6.
        return tenant is { IsActive: true }
            ? Results.Ok(new MyTenantResponse(tenant.Slug, tenant.Name))
            : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Organization not registered");
    }
}
