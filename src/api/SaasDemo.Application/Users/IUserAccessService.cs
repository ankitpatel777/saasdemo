namespace SaasDemo.Application.Users;

/// <summary>What the app knows about the caller in the current tenant.</summary>
public sealed record UserAccess(Guid UserId, string DisplayName, bool IsActive);

/// <summary>
/// "Is this signed-in person an active user of the current tenant?", asked on every protected request.
/// Answers are cached briefly; changing a user's activation must call <see cref="Evict"/> so it applies
/// immediately.
/// </summary>
public interface IUserAccessService
{
    /// <summary>Null when the person has never signed in to this tenant (not provisioned yet).</summary>
    Task<UserAccess?> GetAsync(Guid entraObjectId, CancellationToken ct);

    void Evict(Guid tenantId, Guid entraObjectId);
}
