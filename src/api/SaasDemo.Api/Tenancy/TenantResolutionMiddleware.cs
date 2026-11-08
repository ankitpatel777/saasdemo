using Microsoft.ApplicationInsights.DataContracts;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.Api.Tenancy;

/// <summary>
/// Resolves the tenant for /api/{tenant}/... requests and fills <see cref="TenantContext"/>.
/// Other paths (/api/health, /api/platform, /openapi, /scalar) pass straight through.
/// Runs after authentication and before authorization, so the TenantMember policy can compare
/// the token's tid with the resolved tenant.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, TenantResolver resolver, TenantContext tenantContext)
    {
        // Anonymous callers are never looked up: authorization answers them with 401, so they can't
        // tell real tenants from fake ones by comparing 400/404 responses.
        if (context.User.Identity?.IsAuthenticated != true
            || !TenantRoutePath.TryGetSlug(context.Request.Path, out var slug))
        {
            await next(context);
            return;
        }

        var result = await resolver.ResolveAsync(slug, context.RequestAborted);

        switch (result.Status)
        {
            case TenantResolutionStatus.InvalidSlug:
                await Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid client identifier",
                    detail: "The client identifier in the URL is not valid.").ExecuteAsync(context);
                return;

            case TenantResolutionStatus.NotFound:
                // Same response for unknown and inactive tenants: don't reveal which.
                await Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Client not found").ExecuteAsync(context);
                return;
        }

        tenantContext.Set(result.Tenant!);

        if (context.Features.Get<RequestTelemetry>() is { } telemetry)
        {
            telemetry.Properties["tenant"] = tenantContext.Slug;
        }

        using (logger.BeginScope(new Dictionary<string, object> { ["tenant"] = tenantContext.Slug }))
        {
            await next(context);
        }
    }
}
