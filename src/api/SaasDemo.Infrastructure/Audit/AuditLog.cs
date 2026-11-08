using SaasDemo.Application.Audit;
using SaasDemo.Domain.Audit;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Audit;

/// <summary>Stages events on the request's DbContext; the caller's SaveChanges writes them with its change.</summary>
public sealed class AuditLog(AppDbContext db, TimeProvider timeProvider) : IAuditLog
{
    public void Record(AuditActor actor, AuditAction action, Guid? targetUserId = null, string? details = null) =>
        db.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            tenantId: Guid.Empty, // stamped with the current tenant by AppDbContext
            timeProvider.GetUtcNow().UtcDateTime,
            actor.UserId,
            actor.DisplayName,
            action,
            targetUserId,
            details));
}
