using Microsoft.AspNetCore.Authorization;
using SaasDemo.Application.Users;

namespace SaasDemo.Api.Authorization;

/// <summary>
/// "Has your organization's admin let you in?": the caller must be a provisioned, active user of the
/// tenant in the URL. Roles come from Entra; activation lives in our database (see README "Dashboards").
/// Part of #9: enforced on every TenantUser/TenantAdmin endpoint, not only /me.
/// </summary>
public sealed class ActiveTenantUserRequirement : IAuthorizationRequirement
{
    public const string DeactivatedMessage = "Your account has been deactivated by your organization's administrator.";
    public const string NotProvisionedMessage = "Your account is not set up yet. Reload the app to finish signing in.";
}

/// <summary>
/// Scoped: reads the per-request tenant through <see cref="IUserAccessService"/> (cached for 30 seconds,
/// evicted when an admin changes the user's activation). Registered after the other handlers so it can
/// skip the lookup when the request has already failed (wrong tenant, no role, missing scope).
/// </summary>
public sealed class ActiveTenantUserHandler(IUserAccessService userAccess, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ActiveTenantUserRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveTenantUserRequirement requirement)
    {
        if (context.HasFailed || context.User.GetObjectId() is not { } objectId)
        {
            return; // another requirement already explains the failure, or there is no user to look up
        }

        var ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var access = await userAccess.GetAsync(objectId, ct);

        if (access is null)
        {
            context.Fail(new AuthorizationFailureReason(this, ActiveTenantUserRequirement.NotProvisionedMessage));
        }
        else if (!access.IsActive)
        {
            context.Fail(new AuthorizationFailureReason(this, ActiveTenantUserRequirement.DeactivatedMessage));
        }
        else
        {
            context.Succeed(requirement);
        }
    }
}
