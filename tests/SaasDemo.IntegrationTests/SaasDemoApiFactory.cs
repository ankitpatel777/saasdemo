using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Tenants;
using SaasDemo.Domain.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.IntegrationTests;

/// <summary>
/// Runs the real API with AppDbContext on in-memory SQLite instead of Azure SQL.
/// The schema and data are created BEFORE the host starts, so the Development seeder finds "demo"
/// and does nothing. SQLite proves the EF query filter end to end; RLS needs Azure SQL
/// (see docs/rls-verification.sql).
/// </summary>
public sealed class SaasDemoApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid DemoTenantId = Guid.NewGuid();
    public static readonly Guid OtherTenantId = Guid.NewGuid();

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SaasDemoApiFactory()
    {
        _connection.Open();
        Seed();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            // EF Core 9 keeps provider setup in IDbContextOptionsConfiguration<T>; remove it along with
            // the options, or SQL Server and SQLite would both be registered.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private void Seed()
    {
        var now = DateTime.UtcNow;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using (var db = new AppDbContext(options, new FixedTenantContext(null)))
        {
            db.Database.EnsureCreated();
            db.Tenants.AddRange(
                new Tenant(DemoTenantId, "demo", "Demo Client", Guid.Empty, true, now),
                new Tenant(OtherTenantId, "other", "Other Client", Guid.NewGuid(), true, now),
                new Tenant(Guid.NewGuid(), "paused", "Paused Client", Guid.NewGuid(), false, now));
            db.SaveChanges();
        }

        // Users are tenant-owned: each tenant's users are written with that tenant resolved.
        AddUser(options, DemoTenantId, "alice@demo.example", UserRole.Admin);
        AddUser(options, DemoTenantId, "bob@demo.example", UserRole.User);
        AddUser(options, OtherTenantId, "eve@other.example", UserRole.Admin);
    }

    private static void AddUser(DbContextOptions<AppDbContext> options, Guid tenantId, string email, UserRole role)
    {
        using var db = new AppDbContext(options, new FixedTenantContext(tenantId));
        db.Users.Add(new AppUser(Guid.NewGuid(), tenantId, Guid.NewGuid(), email, email, role, DateTime.UtcNow));
        db.SaveChanges();
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId ?? Guid.Empty;
        public string Slug => string.Empty;
        public string Name => string.Empty;
        public Guid EntraTenantId => Guid.Empty;
        public bool IsResolved => tenantId.HasValue;
    }
}
