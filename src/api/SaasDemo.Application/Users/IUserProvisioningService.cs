using SaasDemo.Domain.Users;

namespace SaasDemo.Application.Users;

/// <summary>Who the caller is, according to their validated Entra token.</summary>
/// <param name="ObjectId">The token's "oid": stable per user per directory.</param>
public sealed record ExternalIdentity(Guid ObjectId, string Email, string DisplayName, UserRole Role);

/// <param name="IsActive">
/// False when the organization deactivated the user: nothing was updated, and callers must deny access.
/// </param>
public sealed record ProvisionedUser(Guid Id, string Email, string DisplayName, UserRole Role, bool IsActive);

/// <summary>
/// Just-in-time provisioning: creates the current tenant's user on first sign-in, and keeps
/// the stored role and last-login time in sync with the token on every call.
/// </summary>
public interface IUserProvisioningService
{
    Task<ProvisionedUser> ProvisionAsync(ExternalIdentity identity, CancellationToken ct);
}
