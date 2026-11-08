using Microsoft.ApplicationInsights.DataContracts;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.Api.Tenancy;

/// <summary>
/// Resolves the tenant for /api/{tenant}/... requests and fills <see cref="TenantContext"/>.
/// Other paths (/api/health, /openapi, /scalar, SPA paths) pass straight through.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, TenantResolver resolver, TenantContext tenantContext)
    {
        if (!TenantRoutePath.TryGetSlug(context.Request.Path, out var slug))
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

        // TODO(step 7.4): once authentication is added (UseAuthentication must run before this
        // middleware), verify the JWT 'tid' claim equals tenantContext.EntraTenantId and return
        // 403 ProblemDetails on mismatch. Until then, the tenant is resolved but NOT authorized.

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
