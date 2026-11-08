using System.Security.Claims;
using SaasDemo.Api.Authorization;
using SaasDemo.Api.Validation;
using SaasDemo.Application.Audit;
using SaasDemo.Application.Common;
using SaasDemo.Application.Dashboard;
using SaasDemo.Application.Tenancy;
using SaasDemo.Application.Users;

namespace SaasDemo.Api.Endpoints;

/// <summary>
/// Admin endpoints under /api/{tenant}. All require TenantAdmin: token for this tenant, Admin role in
/// Entra, and an active user here. None of them filter by tenant: the query filter and RLS do.
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder tenant)
    {
        var admin = tenant.MapGroup(string.Empty).RequireAuthorization(Policies.TenantAdmin);

        admin.MapGet("/dashboard/summary", GetSummary)
            .WithName("GetDashboardSummary");

        admin.MapGet("/users", ListUsers)
            .Validate<UsersQueryParameters>()
            .WithName("GetTenantUsers");

        admin.MapPost("/users/{id:guid}/deactivate", (Guid id, ClaimsPrincipal principal, IUserAccessService access,
                IUserAdministrationService users, CancellationToken ct) =>
                SetActiveAsync(id, active: false, principal, access, users, ct))
            .WithName("DeactivateUser")
            .Produces<UserListItem>();

        admin.MapPost("/users/{id:guid}/reactivate", (Guid id, ClaimsPrincipal principal, IUserAccessService access,
                IUserAdministrationService users, CancellationToken ct) =>
                SetActiveAsync(id, active: true, principal, access, users, ct))
            .WithName("ReactivateUser")
            .Produces<UserListItem>();

        admin.MapGet("/audit", ListAudit)
            .Validate<AuditQueryParameters>()
            .WithName("GetAuditLog");

        admin.MapPut("/settings", UpdateSettings)
            .Validate<UpdateSettingsRequest>()
            .WithName("UpdateTenantSettings")
            .Produces<SettingsResponse>();

        return tenant;
    }

    private static Task<DashboardSummary> GetSummary(IDashboardService dashboard, CancellationToken ct) =>
        dashboard.GetSummaryAsync(ct);

    private static Task<PagedResult<UserListItem>> ListUsers(
        [AsParameters] UsersQueryParameters query, IUserReadService users, CancellationToken ct) =>
        users.ListAsync(query.ToQuery(), ct);

    private static async Task<IResult> SetActiveAsync(
        Guid id,
        bool active,
        ClaimsPrincipal principal,
        IUserAccessService access,
        IUserAdministrationService users,
        CancellationToken ct)
    {
        var actor = await principal.GetAuditActorAsync(access, ct);
        var change = await users.SetActiveAsync(id, active, actor, ct);

        return change.Outcome switch
        {
            UserStatusChangeOutcome.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "User not found", detail: "No user with this id exists in your organization."),
            UserStatusChangeOutcome.CannotChangeSelf => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid operation",
                detail: active ? "You cannot reactivate yourself" : "You cannot deactivate yourself"),
            _ => Results.Ok(change.User),
        };
    }

    private static async Task<PagedResult<AuditEntry>> ListAudit(
        [AsParameters] AuditQueryParameters query, IAuditReadService audit, CancellationToken ct) =>
        await audit.ListAsync(query.Page ?? 1, query.PageSize ?? AuditQueryParameters.DefaultPageSize, ct);

    private static async Task<IResult> UpdateSettings(
        UpdateSettingsRequest request,
        ClaimsPrincipal principal,
        IUserAccessService access,
        ITenantSettingsService settings,
        CancellationToken ct)
    {
        var actor = await principal.GetAuditActorAsync(access, ct);
        try
        {
            var name = await settings.RenameAsync(request.Name!, actor, ct);
            return Results.Ok(new SettingsResponse(name));
        }
        catch (ArgumentException ex)
        {
            // The validator already checks this; the domain rule is the last line of defence.
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name", detail: ex.Message);
        }
    }
}
