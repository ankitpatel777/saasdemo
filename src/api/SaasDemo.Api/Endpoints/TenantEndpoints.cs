using System.Security.Claims;
using SaasDemo.Api.Authorization;
using SaasDemo.Application.Audit;
using SaasDemo.Application.Tenancy;
using SaasDemo.Application.Users;

namespace SaasDemo.Api.Endpoints;

/// <summary>Deliberately excludes EntraTenantId: the directory id is never exposed to clients.</summary>
public sealed record TenantInfoResponse(Guid TenantId, string Slug, string Name);

public sealed record TenantRef(string Slug, string Name);

public sealed record MeResponse(Guid Id, string Email, string DisplayName, string Role, TenantRef Tenant);

public sealed record SignInEvent(DateTime OccurredAtUtc);

/// <summary>
/// Endpoints under /api/{tenant}. TenantResolutionMiddleware resolves the tenant first; the policies
/// then require the token's tid to match it, and (except /me) an active user of the tenant.
/// </summary>
public static class TenantEndpoints
{
    public const int RecentSignInCount = 10;

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

        tenant.MapGet("/me", GetMe)
            .RequireAuthorization(Policies.TenantSignIn)
            .WithName("GetMe");

        tenant.MapGet("/me/activity", GetMyActivity)
            .RequireAuthorization(Policies.TenantUser)
            .WithName("GetMyActivity");

        tenant.MapAdminEndpoints();

        return app;
    }

    private static TenantInfoResponse GetInfo(ITenantContext tenantContext) =>
        new(tenantContext.TenantId, tenantContext.Slug, tenantContext.Name);

    /// <summary>
    /// Just-in-time provisioning: creates the user on first call and re-syncs the role on every call.
    /// The only tenant endpoint open to a user who is not provisioned yet (policy TenantSignIn).
    /// </summary>
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
            // The policy already guarantees a role; this keeps the endpoint safe on its own.
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden", detail: "No role assigned in your organization.");
        }

        var email = principal.FindFirstValue(EntraClaims.PreferredUsername)
            ?? principal.FindFirstValue(EntraClaims.Email)
            ?? objectId.ToString();
        var displayName = principal.FindFirstValue(EntraClaims.Name) ?? email;

        var user = await provisioning.ProvisionAsync(new ExternalIdentity(objectId, email, displayName, role), ct);

        if (!user.IsActive)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden",
                detail: ActiveTenantUserRequirement.DeactivatedMessage);
        }

        return Results.Ok(new MeResponse(
            user.Id, user.Email, user.DisplayName, user.Role.ToString(),
            new TenantRef(tenantContext.Slug, tenantContext.Name)));
    }

    /// <summary>The caller's own recent sign-ins, newest first.</summary>
    private static async Task<IReadOnlyList<SignInEvent>> GetMyActivity(
        ClaimsPrincipal principal,
        IUserAccessService access,
        IAuditReadService audit,
        CancellationToken ct)
    {
        var actor = await principal.GetAuditActorAsync(access, ct);
        var signIns = await audit.GetRecentSignInsAsync(actor.UserId!.Value, RecentSignInCount, ct);
        return signIns.Select(at => new SignInEvent(at)).ToList();
    }
}

internal static class AuditActorExtensions
{
    /// <summary>
    /// The caller as an audit actor. Only for endpoints behind TenantUser/TenantAdmin, whose
    /// ActiveTenantUser requirement has just loaded (and cached) this lookup.
    /// </summary>
    public static async Task<AuditActor> GetAuditActorAsync(
        this ClaimsPrincipal principal, IUserAccessService access, CancellationToken ct)
    {
        var objectId = principal.GetObjectId()
            ?? throw new InvalidOperationException("Authorized requests always carry an 'oid' claim.");
        var user = await access.GetAsync(objectId, ct)
            ?? throw new InvalidOperationException("Authorized requests always belong to a provisioned user.");
        return new AuditActor(user.UserId, user.DisplayName);
    }
}
