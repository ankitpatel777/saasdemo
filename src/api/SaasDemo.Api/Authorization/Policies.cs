namespace SaasDemo.Api.Authorization;

/// <summary>Authorization policy names, app roles and the delegated scope this API exposes.</summary>
public static class Policies
{
    /// <summary>Token carries the "access_as_user" delegated scope.</summary>
    public const string ApiScope = nameof(ApiScope);

    /// <summary>ApiScope + the token's tenant (tid) owns the tenant in the URL.</summary>
    public const string TenantMember = nameof(TenantMember);

    /// <summary>TenantMember + app role Admin or User.</summary>
    public const string TenantUser = nameof(TenantUser);

    /// <summary>TenantMember + app role Admin.</summary>
    public const string TenantAdmin = nameof(TenantAdmin);

    public const string RequiredScope = "access_as_user";
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>Entra v2 claim types, used unmapped (MapInboundClaims = false).</summary>
public static class EntraClaims
{
    public const string TenantId = "tid";
    public const string ObjectId = "oid";
    public const string Scope = "scp";
    public const string Roles = "roles";
    public const string Name = "name";
    public const string PreferredUsername = "preferred_username";
    public const string Email = "email";
}
