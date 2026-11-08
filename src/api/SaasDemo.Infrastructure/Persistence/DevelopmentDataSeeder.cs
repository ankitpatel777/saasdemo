using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaasDemo.Domain.Tenants;

namespace SaasDemo.Infrastructure.Persistence;

/// <summary>
/// Development only (registered in Program.cs when the environment is Development): makes sure the
/// "demo" tenant exists. Its Entra tenant id comes from "Dev:DemoEntraTenantId" (user secrets),
/// defaulting to an all-zero placeholder, so no real id is ever committed.
/// It does not run migrations: apply them with "dotnet ef database update".
/// </summary>
public sealed class DevelopmentDataSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<DevelopmentDataSeeder> logger) : IHostedService
{
    private const string DemoSlug = "demo";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (await db.Tenants.AnyAsync(t => t.Slug == DemoSlug, cancellationToken))
            {
                return;
            }

            var entraTenantId = configuration.GetValue("Dev:DemoEntraTenantId", Guid.Empty);
            db.Tenants.Add(new Tenant(
                Guid.NewGuid(), DemoSlug, "Demo Client", entraTenantId, isActive: true,
                timeProvider.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Seeded development tenant {Slug}", DemoSlug);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Keep local startup going; /api/health/ready reports the database problem.
            logger.LogError(ex,
                "Development seeding failed. Is ConnectionStrings:DefaultConnection set in user secrets and the database migrated?");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
