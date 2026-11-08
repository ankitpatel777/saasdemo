using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Audit;
using SaasDemo.Application.Common;
using SaasDemo.Domain.Audit;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Audit;

/// <summary>No tenant WHERE clause: the query filters on both tables (and RLS) scope it to the current tenant.</summary>
public sealed class AuditReadService(AppDbContext db) : IAuditReadService
{
    public async Task<PagedResult<AuditEntry>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var events = db.AuditEvents.AsNoTracking();
        var total = await events.CountAsync(ct);

        // Left join for the target's current name: one query, no N+1.
        var rows = await (
                from e in events
                join u in db.Users.AsNoTracking() on e.TargetUserId equals u.Id into targets
                from target in targets.DefaultIfEmpty()
                orderby e.OccurredAtUtc descending, e.Id
                select new
                {
                    e.Id,
                    e.OccurredAtUtc,
                    e.Action,
                    e.ActorUserId,
                    e.ActorDisplayName,
                    e.TargetUserId,
                    TargetDisplayName = target == null ? null : target.DisplayName,
                    e.Details,
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.ConvertAll(r => new AuditEntry(
            r.Id, r.OccurredAtUtc, r.Action.ToString(), r.ActorUserId, r.ActorDisplayName,
            r.TargetUserId, r.TargetDisplayName, r.Details));

        return new PagedResult<AuditEntry>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<DateTime>> GetRecentSignInsAsync(Guid userId, int count, CancellationToken ct) =>
        await db.AuditEvents
            .AsNoTracking()
            .Where(e => e.TargetUserId == userId && e.Action == AuditAction.UserSignedIn)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => e.OccurredAtUtc)
            .Take(count)
            .ToListAsync(ct);
}
