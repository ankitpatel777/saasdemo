using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Audit;
using SaasDemo.Application.Users;
using SaasDemo.Domain.Audit;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Users;

public sealed class UserAdministrationService(AppDbContext db, IAuditLog auditLog, IUserAccessService access)
    : IUserAdministrationService
{
    public async Task<UserStatusChange> SetActiveAsync(Guid userId, bool active, AuditActor actor, CancellationToken ct)
    {
        // Another tenant's user is invisible here (query filter + RLS), so it is simply "not found".
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return new UserStatusChange(UserStatusChangeOutcome.NotFound, null);
        }

        if (user.Id == actor.UserId)
        {
            return new UserStatusChange(UserStatusChangeOutcome.CannotChangeSelf, null);
        }

        if (user.IsActive == active)
        {
            return new UserStatusChange(UserStatusChangeOutcome.Unchanged, UserReadService.ToListItem(user));
        }

        if (active)
        {
            user.Reactivate();
        }
        else
        {
            user.Deactivate();
        }

        auditLog.Record(actor, active ? AuditAction.UserReactivated : AuditAction.UserDeactivated, user.Id);
        await db.SaveChangesAsync(ct);

        // After the save, so the user's next request re-reads the new state from the database.
        access.Evict(user.TenantId, user.EntraObjectId);

        return new UserStatusChange(UserStatusChangeOutcome.Changed, UserReadService.ToListItem(user));
    }
}
