using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaasDemo.Application.Tenancy;
using SaasDemo.Application.Users;
using SaasDemo.Infrastructure.Persistence;
using SaasDemo.Infrastructure.Tenancy;
using SaasDemo.Infrastructure.Users;

namespace SaasDemo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Health-check tag for checks that need external dependencies (mapped to /api/health/ready).</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Passwordless (Authentication=Active Directory Default). Locally from user secrets,
        // in Azure from the App Service setting ConnectionStrings__DefaultConnection (Key Vault reference).
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.AddScoped<TenantSessionContextInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString) ? null : connectionString, sql => sql
                // Serverless Azure SQL may be paused, and resuming can take up to about a minute.
                // 6 retries with exponential backoff capped at 10s gives a ~60s total budget.
                .EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)
                .CommandTimeout(60))
            .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

        services.AddScoped<EfTenantStore>();
        services.AddScoped<ITenantStore, CachedTenantStore>();
        services.AddScoped<IUserReadService, UserReadService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: [ReadyTag]);

        return services;
    }
}
