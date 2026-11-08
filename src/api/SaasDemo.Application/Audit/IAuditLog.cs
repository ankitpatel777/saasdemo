using SaasDemo.Domain.Audit;

namespace SaasDemo.Application.Audit;

/// <summary>Who did it: the signed-in user as the app knows them.</summary>
public sealed record AuditActor(Guid? UserId, string DisplayName);

/// <summary>
/// Writes audit events for the current tenant. <see cref="Record"/> only stages the event: it is saved
/// together with the change it describes (same SaveChanges, same transaction), so the log can't
/// claim something that didn't happen, or miss something that did.
/// </summary>
public interface IAuditLog
{
    void Record(AuditActor actor, AuditAction action, Guid? targetUserId = null, string? details = null);
}
