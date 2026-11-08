using Microsoft.AspNetCore.Authorization;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.Api.Authorization;

/// <summary>
/// "Do you belong here?": the token's tenant (tid) must be the Entra directory linked to the tenant
/// resolved from the URL. Authentication alone only proves who the caller is; any Entra tenant can
/// sign in to this multi-tenant app.
/// </summary>
public sealed class TenantMemberRequirement : IAuthorizationRequirement;

/// <summary>Scoped: it reads the per-request <see cref="ITenantContext"/>.</summary>
public sealed class TenantMemberHandler(ITenantContext tenantContext, ILogger<TenantMemberHandler> logger)
    : AuthorizationHandler<TenantMemberRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantMemberRequirement requirement)
    {
        if (!tenantContext.IsResolved)
        {
            context.Fail(new AuthorizationFailureReason(this, "This address does not belong to an organization."));
            return Task.CompletedTask;
        }

        var tokenTenantId = context.User.GetEntraTenantId();

        if (tokenTenantId == tenantContext.EntraTenantId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Log the identifiers only, never the token.
        logger.LogWarning(
            "Tenant mismatch: URL tenant {TenantSlug}, token tid {TokenTenantId}",
            tenantContext.Slug,
            tokenTenantId?.ToString() ?? "(missing)");

        context.Fail(new AuthorizationFailureReason(this, "Your organization does not have access to this client."));
        return Task.CompletedTask;
    }
}
