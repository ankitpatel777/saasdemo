using SaasDemo.Domain.Common;

namespace SaasDemo.Domain.Audit;

/// <summary>
/// An append-only record of something that happened in a tenant. Tenant-owned, so it is isolated by the
/// EF query filter and by SQL Row-Level Security like every other tenant table.
/// </summary>
public sealed class AuditEvent : ITenantOwned
{
    public const int ActorDisplayNameMaxLength = 200;
    public const int DetailsMaxLength = 500;

    /// <param name="tenantId">Pass <see cref="Guid.Empty"/> to let persistence stamp the current tenant on save.</param>
    public AuditEvent(
        Guid id,
        Guid tenantId,
        DateTime occurredAtUtc,
        Guid? actorUserId,
        string actorDisplayName,
        AuditAction action,
        Guid? targetUserId,
        string? details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);

        Id = id;
        TenantId = tenantId;
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        ActorDisplayName = Truncate(actorDisplayName, ActorDisplayNameMaxLength);
        Action = action;
        TargetUserId = targetUserId;
        Details = details is null ? null : Truncate(details, DetailsMaxLength);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Null for events without a signed-in actor (none today; kept for system jobs).</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>A copy of the name at the time, so the log still reads well if the user changes later.</summary>
    public string ActorDisplayName { get; private set; }

    public AuditAction Action { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public string? Details { get; private set; }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
