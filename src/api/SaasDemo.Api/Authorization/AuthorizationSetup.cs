using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace SaasDemo.Api.Authorization;

public static class AuthorizationSetup
{
    /// <summary>
    /// Entra ID bearer tokens via Microsoft.Identity.Web, bound to "AzureAd". With TenantId
    /// "organizations", Microsoft.Identity.Web validates the issuer for ANY Entra tenant (the issuer
    /// must match the token's own tid). Which tenant may use which client is decided later, by the
    /// TenantMember policy.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration, "AzureAd");

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            // Keep Entra claim names ("tid", "oid", "roles", "scp") instead of legacy WS-Fed URIs.
            options.MapInboundClaims = false;

            var clientId = configuration["AzureAd:ClientId"];
            options.TokenValidationParameters.ValidAudiences = [clientId, $"api://{clientId}"];
            options.TokenValidationParameters.NameClaimType = EntraClaims.Name;
            options.TokenValidationParameters.RoleClaimType = EntraClaims.Roles;
        });

        return services;
    }

    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        var scope = new ScopeRequirement(Policies.RequiredScope);
        var member = new TenantMemberRequirement();
        var anyRole = new AppRoleRequirement("No role assigned in your organization.", AppRoles.Admin, AppRoles.User);
        var adminRole = new AppRoleRequirement("This action requires the Admin role.", AppRoles.Admin);
        var active = new ActiveTenantUserRequirement();

        services.AddAuthorizationBuilder()
            // Anything without its own policy (including future endpoints) needs a signed-in caller with
            // the API scope. Health and OpenAPI/Scalar opt out with AllowAnonymous.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(scope)
                .Build())
            .AddPolicy(Policies.ApiScope, p => p.RequireAuthenticatedUser().AddRequirements(scope))
            .AddPolicy(Policies.TenantMember, p => p.RequireAuthenticatedUser().AddRequirements(scope, member))
            // /me only: the one endpoint a not-yet-provisioned user may call (it provisions them).
            .AddPolicy(Policies.TenantSignIn, p => p.RequireAuthenticatedUser().AddRequirements(scope, member, anyRole))
            .AddPolicy(Policies.TenantUser, p => p.RequireAuthenticatedUser().AddRequirements(scope, member, anyRole, active))
            .AddPolicy(Policies.TenantAdmin, p => p.RequireAuthenticatedUser().AddRequirements(scope, member, adminRole, active));

        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
        services.AddSingleton<IAuthorizationHandler, AppRoleHandler>();
        services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();
        // Last: it skips its database lookup when an earlier handler has already failed the request.
        services.AddScoped<IAuthorizationHandler, ActiveTenantUserHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();

        return services;
    }
}
