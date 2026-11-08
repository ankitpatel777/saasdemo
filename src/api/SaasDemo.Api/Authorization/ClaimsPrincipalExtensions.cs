using System.Security.Claims;
using SaasDemo.Domain.Users;

namespace SaasDemo.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetEntraTenantId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(EntraClaims.TenantId), out var tid) ? tid : null;

    public static Guid? GetObjectId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(EntraClaims.ObjectId), out var oid) ? oid : null;

    /// <summary>"scp" is one space-separated claim in v2 tokens.</summary>
    public static bool HasScope(this ClaimsPrincipal user, string scope) =>
        user.FindAll(EntraClaims.Scope)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);

    public static bool HasAppRole(this ClaimsPrincipal user, string role) =>
        user.FindAll(EntraClaims.Roles).Any(c => string.Equals(c.Value, role, StringComparison.Ordinal));

    /// <summary>The app role from the token; Admin wins when both are assigned. Null when neither is.</summary>
    public static UserRole? GetAppRole(this ClaimsPrincipal user) =>
        user.HasAppRole(AppRoles.Admin) ? UserRole.Admin
        : user.HasAppRole(AppRoles.User) ? UserRole.User
        : null;
}
