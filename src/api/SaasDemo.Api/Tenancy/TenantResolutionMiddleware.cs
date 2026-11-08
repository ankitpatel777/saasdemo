using System.Security.Claims;

namespace SaasDemo.Api.Tenancy;

public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var segments = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments is not { Length: >= 3 } ||
            !string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var configuredTenantId = configuration[$"Tenants:{segments[1]}"];
        if (!Guid.TryParse(configuredTenantId, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        tenantContext.Resolve(tenantId);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claimValue = context.User.FindFirstValue("tid");
            if (!Guid.TryParse(claimValue, out var tokenTenantId) || tokenTenantId != tenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }
}
