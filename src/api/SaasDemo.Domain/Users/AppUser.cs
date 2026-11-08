using SaasDemo.Domain.Common;

namespace SaasDemo.Domain.Users;

/// <summary>A person signed in through Entra ID, scoped to one tenant.</summary>
public sealed class AppUser : ITenantOwned
{
    /// <param name="tenantId">
    /// Pass <see cref="Guid.Empty"/> to let persistence stamp the current tenant on save.
    /// </param>
    public AppUser(
        Guid id,
        Guid tenantId,
        Guid entraObjectId,
        string email,
        string displayName,
        UserRole role,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        TenantId = tenantId;
        EntraObjectId = entraObjectId;
        Email = email;
        DisplayName = displayName;
        Role = role;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EntraObjectId { get; private set; }
    public string Email { get; private set; }
    public string DisplayName { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public void RecordLogin(DateTime utcNow) => LastLoginAtUtc = utcNow;

    /// <summary>The token is the source of truth for roles; the stored role is a synced copy.</summary>
    public void SyncRole(UserRole role) => Role = role;

    public void Deactivate() => IsActive = false;
}
