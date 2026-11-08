using SaasDemo.Application.Common;

namespace SaasDemo.Application.Audit;

/// <param name="TargetDisplayName">The target user's current name, when the event has a target.</param>
public sealed record AuditEntry(
    Guid Id,
    DateTime OccurredAtUtc,
    string Action,
    Guid? ActorUserId,
    string ActorDisplayName,
    Guid? TargetUserId,
    string? TargetDisplayName,
    string? Details);

/// <summary>Reads the current tenant's audit log. Isolation comes from the query filter and RLS.</summary>
public interface IAuditReadService
{
    /// <summary>Newest first.</summary>
    Task<PagedResult<AuditEntry>> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>When the user last signed in, newest first.</summary>
    Task<IReadOnlyList<DateTime>> GetRecentSignInsAsync(Guid userId, int count, CancellationToken ct);
}
