using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaasDemo.Application.Tenancy;
using SaasDemo.Infrastructure.Tenancy;

namespace SaasDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenantStoreOptions>(configuration.GetSection(TenantStoreOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITenantStore, InMemoryTenantStore>();

        return services;
    }
}
