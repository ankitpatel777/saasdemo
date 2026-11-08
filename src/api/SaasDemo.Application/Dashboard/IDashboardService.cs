namespace SaasDemo.Application.Dashboard;

public sealed record DashboardSummary(int TotalUsers, int ActiveUsers, int Admins, int SignInsLast7Days);

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct);
}
