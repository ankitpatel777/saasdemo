using SaasDemo.Application.Tenancy;

namespace SaasDemo.Api.Endpoints;

/// <summary>Deliberately excludes EntraTenantId: the directory id is never exposed to clients.</summary>
public sealed record TenantInfoResponse(Guid TenantId, string Slug, string Name);

public static class TenantInfoEndpoints
{
    /// <summary>
    /// GET /api/{tenant}/info. Development-only diagnostic: it is anonymous, so mapping it in other
    /// environments would let anyone enumerate tenants by changing the slug. Program.cs maps it only
    /// when the environment is Development.
    /// </summary>
    public static IEndpointRouteBuilder MapTenantInfoEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/{tenant}/info", (ITenantContext tenantContext) =>
                // Defence in depth: reserved slugs (e.g. /api/admin/info) resolve no tenant.
                tenantContext.IsResolved
                    ? Results.Ok(new TenantInfoResponse(tenantContext.TenantId, tenantContext.Slug, tenantContext.Name))
                    : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found"))
            .WithName("GetTenantInfo")
            .Produces<TenantInfoResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
