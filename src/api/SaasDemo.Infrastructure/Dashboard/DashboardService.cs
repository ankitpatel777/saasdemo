using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Dashboard;
using SaasDemo.Domain.Audit;
using SaasDemo.Domain.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Dashboard;

/// <summary>Counts for the current tenant only (query filter + RLS); no tenant WHERE clause here.</summary>
public sealed class DashboardService(AppDbContext db, TimeProvider timeProvider) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        var total = await users.CountAsync(ct);
        var active = await users.CountAsync(u => u.IsActive, ct);
        var admins = await users.CountAsync(u => u.Role == UserRole.Admin, ct);

        var since = timeProvider.GetUtcNow().UtcDateTime.AddDays(-7);
        var signIns = await db.AuditEvents
            .AsNoTracking()
            .CountAsync(e => e.Action == AuditAction.UserSignedIn && e.OccurredAtUtc >= since, ct);

        return new DashboardSummary(total, active, admins, signIns);
    }
}
