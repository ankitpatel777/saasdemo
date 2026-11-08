using SaasDemo.Application.Tenancy;
using SaasDemo.Application.Users;

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

    /// <summary>
    /// GET /api/{tenant}/users. Development-only proof of tenant isolation: the handler has no tenant
    /// WHERE clause, yet only the current tenant's users come back (EF query filter + SQL RLS).
    /// TODO(#5, step 7.4): expose outside Development only behind [Authorize] (Admin) with the tid check.
    /// </summary>
    public static IEndpointRouteBuilder MapTenantUsersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/{tenant}/users", async (ITenantContext tenantContext, IUserReadService users, CancellationToken ct) =>
                tenantContext.IsResolved
                    ? Results.Ok(await users.ListAsync(ct))
                    : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found"))
            .WithName("GetTenantUsers")
            .Produces<IReadOnlyList<UserSummary>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
