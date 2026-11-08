namespace SaasDemo.Domain.Audit;

/// <summary>What happened. Stored as text, so values may be added but never renamed.</summary>
public enum AuditAction
{
    UserSignedIn,
    UserDeactivated,
    UserReactivated,
    TenantSettingsUpdated,
    UserProvisioned,
}
