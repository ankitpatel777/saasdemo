using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Users;
using SaasDemo.Domain.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Users;

/// <summary>
/// Upserts the caller as an <see cref="AppUser"/> of the current tenant, keyed by (TenantId, oid).
/// The tenant is never passed in: the query filter scopes the lookup, and the SaveChanges guard
/// stamps the current tenant on new users.
/// </summary>
public sealed class UserProvisioningService(AppDbContext db, TimeProvider timeProvider) : IUserProvisioningService
{
    private const int EmailMaxLength = 256;
    private const int DisplayNameMaxLength = 200;

    public async Task<ProvisionedUser> ProvisionAsync(ExternalIdentity identity, CancellationToken ct)
    {
        try
        {
            return await UpsertAsync(identity, ct);
        }
        catch (DbUpdateException)
        {
            // Two first calls raced and the other one inserted the user (unique (TenantId, EntraObjectId)).
            // Drop our failed insert and update the row that won.
            db.ChangeTracker.Clear();
            return await UpsertAsync(identity, ct);
        }
    }

    private async Task<ProvisionedUser> UpsertAsync(ExternalIdentity identity, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await db.Users.SingleOrDefaultAsync(u => u.EntraObjectId == identity.ObjectId, ct);

        if (user is null)
        {
            user = new AppUser(
                Guid.NewGuid(),
                tenantId: Guid.Empty, // stamped with the current tenant by AppDbContext
                identity.ObjectId,
                Truncate(identity.Email, EmailMaxLength),
                Truncate(identity.DisplayName, DisplayNameMaxLength),
                identity.Role,
                now);
            db.Users.Add(user);
        }
        else
        {
            user.SyncRole(identity.Role);
        }

        user.RecordLogin(now);
        await db.SaveChangesAsync(ct);

        return new ProvisionedUser(user.Id, user.Email, user.DisplayName, user.Role);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
