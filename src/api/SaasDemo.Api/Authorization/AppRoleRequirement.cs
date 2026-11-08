using Microsoft.AspNetCore.Authorization;

namespace SaasDemo.Api.Authorization;

/// <summary>
/// The token's "roles" claim must contain one of <see cref="AllowedRoles"/>. Like RequireRole, but
/// it explains the failure, so the 403 ProblemDetails can tell the user what is missing.
/// </summary>
public sealed class AppRoleRequirement(string failureMessage, params string[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyList<string> AllowedRoles { get; } = allowedRoles;
    public string FailureMessage { get; } = failureMessage;
}

public sealed class AppRoleHandler : AuthorizationHandler<AppRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AppRoleRequirement requirement)
    {
        if (requirement.AllowedRoles.Any(context.User.HasAppRole))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, requirement.FailureMessage));
        }

        return Task.CompletedTask;
    }
}
