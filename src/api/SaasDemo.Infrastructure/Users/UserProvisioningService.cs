using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Audit;
using SaasDemo.Application.Users;
using SaasDemo.Domain.Audit;
using SaasDemo.Domain.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Users;

/// <summary>
/// Upserts the caller as an <see cref="AppUser"/> of the current tenant, keyed by (TenantId, oid).
/// The tenant is never passed in: the query filter scopes the lookup, and the SaveChanges guard
/// stamps the current tenant on new users. Audits UserProvisioned on the first call and UserSignedIn
/// at most once per <see cref="SignInAuditInterval"/> per user (the SPA calls /me on every app load).
/// </summary>
public sealed class UserProvisioningService(AppDbContext db, TimeProvider timeProvider, IAuditLog auditLog)
    : IUserProvisioningService
{
    public static readonly TimeSpan SignInAuditInterval = TimeSpan.FromMinutes(30);

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
            // Drop our failed insert (and its audit events) and update the row that won.
            db.ChangeTracker.Clear();
            return await UpsertAsync(identity, ct);
        }
    }

    private async Task<ProvisionedUser> UpsertAsync(ExternalIdentity identity, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await db.Users.SingleOrDefaultAsync(u => u.EntraObjectId == identity.ObjectId, ct);
        bool recordSignIn;

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
            auditLog.Record(Actor(user), AuditAction.UserProvisioned, user.Id);
            recordSignIn = true;
        }
        else if (!user.IsActive)
        {
            // Deactivated by the organization: record nothing (no role sync, no login time), just report it.
            return ToResult(user);
        }
        else
        {
            user.SyncRole(identity.Role);
            var since = now - SignInAuditInterval;
            recordSignIn = !await db.AuditEvents.AnyAsync(
                e => e.TargetUserId == user.Id && e.Action == AuditAction.UserSignedIn && e.OccurredAtUtc > since, ct);
        }

        if (recordSignIn)
        {
            auditLog.Record(Actor(user), AuditAction.UserSignedIn, user.Id);
        }

        user.RecordLogin(now);
        await db.SaveChangesAsync(ct);

        return ToResult(user);
    }

    private static AuditActor Actor(AppUser user) => new(user.Id, user.DisplayName);

    private static ProvisionedUser ToResult(AppUser user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
