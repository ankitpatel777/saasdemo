using System.Security.Claims;
using SaasDemo.Api.Authorization;
using SaasDemo.Application.Tenancy;
using SaasDemo.Application.Users;

namespace SaasDemo.Api.Endpoints;

/// <summary>Deliberately excludes EntraTenantId: the directory id is never exposed to clients.</summary>
public sealed record TenantInfoResponse(Guid TenantId, string Slug, string Name);

public sealed record TenantRef(string Slug, string Name);

public sealed record MeResponse(Guid Id, string Email, string DisplayName, string Role, TenantRef Tenant);

/// <summary>
/// Endpoints under /api/{tenant}. TenantResolutionMiddleware resolves the tenant first; the
/// TenantUser/TenantAdmin policies then require the token's tid to match it.
/// </summary>
public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var tenant = app.MapGroup("/api/{tenant}")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tenant.MapGet("/info", GetInfo)
            .RequireAuthorization(Policies.TenantUser)
            .WithName("GetTenantInfo");

        tenant.MapGet("/users", ListUsers)
            .RequireAuthorization(Policies.TenantAdmin)
            .WithName("GetTenantUsers");

        tenant.MapGet("/me", GetMe)
            .RequireAuthorization(Policies.TenantUser)
            .WithName("GetMe");

        return app;
    }

    private static TenantInfoResponse GetInfo(ITenantContext tenantContext) =>
        new(tenantContext.TenantId, tenantContext.Slug, tenantContext.Name);

    /// <summary>No tenant WHERE clause: the EF query filter and SQL RLS scope it to the current tenant.</summary>
    private static Task<IReadOnlyList<UserSummary>> ListUsers(IUserReadService users, CancellationToken ct) =>
        users.ListAsync(ct);

    /// <summary>Just-in-time provisioning: creates the user on first call and re-syncs the role on every call.</summary>
    private static async Task<IResult> GetMe(
        ClaimsPrincipal principal,
        ITenantContext tenantContext,
        IUserProvisioningService provisioning,
        CancellationToken ct)
    {
        if (principal.GetObjectId() is not { } objectId)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid token", detail: "The access token has no 'oid' claim.");
        }

        if (principal.GetAppRole() is not { } role)
        {
            // The TenantUser policy already guarantees a role; this keeps the endpoint safe on its own.
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden", detail: "No role assigned in your organization.");
        }

        var email = principal.FindFirstValue(EntraClaims.PreferredUsername)
            ?? principal.FindFirstValue(EntraClaims.Email)
            ?? objectId.ToString();
        var displayName = principal.FindFirstValue(EntraClaims.Name) ?? email;

        var user = await provisioning.ProvisionAsync(new ExternalIdentity(objectId, email, displayName, role), ct);

        return Results.Ok(new MeResponse(
            user.Id, user.Email, user.DisplayName, user.Role.ToString(),
            new TenantRef(tenantContext.Slug, tenantContext.Name)));
    }
}
