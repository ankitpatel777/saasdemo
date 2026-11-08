using Microsoft.AspNetCore.Authorization;

namespace SaasDemo.Api.Authorization;

/// <summary>The access token must carry the given delegated scope ("scp").</summary>
public sealed record ScopeRequirement(string Scope) : IAuthorizationRequirement;

public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (context.User.HasScope(requirement.Scope))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, $"The access token is missing the required scope '{requirement.Scope}'."));
        }

        return Task.CompletedTask;
    }
}
