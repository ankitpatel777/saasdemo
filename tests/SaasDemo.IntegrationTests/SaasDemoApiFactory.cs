using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Audit;
using SaasDemo.Domain.Tenants;
using SaasDemo.Domain.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.IntegrationTests;

/// <summary>
/// Runs the real API with:
/// - AppDbContext on in-memory SQLite instead of Azure SQL (schema and data created before the host starts);
/// - a test authentication scheme instead of Entra JWT validation (see <see cref="TestAuthHandler"/>).
/// Everything else (tenant resolution, policies, endpoints) is the production code.
/// SQLite proves the EF query filter end to end; RLS needs Azure SQL (see docs/rls-verification.sql).
/// </summary>
public sealed class SaasDemoApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid DemoTenantId = Guid.NewGuid();
    public static readonly Guid DemoEntraTenantId = Guid.NewGuid();
    public static readonly Guid OtherTenantId = Guid.NewGuid();
    public static readonly Guid OtherEntraTenantId = Guid.NewGuid();
    public static readonly Guid PausedEntraTenantId = Guid.NewGuid();

    // Seeded users, by their Entra object id (oid), so tests can call the API as them.
    public static readonly Guid AliceObjectId = Guid.NewGuid(); // demo Admin
    public static readonly Guid BobObjectId = Guid.NewGuid();   // demo User
    public static readonly Guid EveObjectId = Guid.NewGuid();   // other Admin

    public static TestUser Alice => new(DemoEntraTenantId, AliceObjectId, ["Admin"], Name: "Alice", Email: "alice@demo.example");
    public static TestUser Bob => new(DemoEntraTenantId, BobObjectId, ["User"], Name: "Bob", Email: "bob@demo.example");
    public static TestUser Eve => new(OtherEntraTenantId, EveObjectId, ["Admin"], Name: "Eve", Email: "eve@other.example");

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public SaasDemoApiFactory()
    {
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        Seed();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Pin the demo tenant's directory: otherwise the Development seeder would re-link "demo" to
        // whatever Dev:DemoEntraTenantId is in the developer's user secrets.
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dev:DemoEntraTenantId"] = DemoEntraTenantId.ToString(),
        }));

        builder.ConfigureTestServices(services =>
        {
            // EF Core 9 keeps provider setup in IDbContextOptionsConfiguration<T>; remove it along with
            // the options, or SQL Server and SQLite would both be registered.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    /// <summary>Reads users of one tenant straight from the database (bypassing the API).</summary>
    public List<AppUser> GetUsers(Guid tenantId)
    {
        using var db = new AppDbContext(_dbOptions, new FixedTenantContext(tenantId));
        return db.Users.AsNoTracking().ToList();
    }

    /// <summary>Deactivates a user directly in the database, as an organization admin would.</summary>
    public void DeactivateUser(Guid tenantId, Guid entraObjectId)
    {
        using var db = new AppDbContext(_dbOptions, new FixedTenantContext(tenantId));
        db.Users.Single(u => u.EntraObjectId == entraObjectId).Deactivate();
        db.SaveChanges();
    }

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

        using (var db = new AppDbContext(_dbOptions, new FixedTenantContext(null)))
        {
            db.Database.EnsureCreated();
            db.Tenants.AddRange(
                new Tenant(DemoTenantId, "demo", "Demo Client", DemoEntraTenantId, true, now),
                new Tenant(OtherTenantId, "other", "Other Client", OtherEntraTenantId, true, now),
                new Tenant(Guid.NewGuid(), "paused", "Paused Client", PausedEntraTenantId, false, now));
            db.SaveChanges();
        }

        // Users are tenant-owned: each tenant's users are written with that tenant resolved.
        AddUser(DemoTenantId, "alice@demo.example", "Alice", UserRole.Admin, AliceObjectId);
        AddUser(DemoTenantId, "bob@demo.example", "Bob", UserRole.User, BobObjectId);
        AddUser(OtherTenantId, "eve@other.example", "Eve", UserRole.Admin, EveObjectId);
    }

    /// <summary>Adds a provisioned user straight to the database. Returns it (Id and EntraObjectId set).</summary>
    public AppUser AddUser(
        Guid tenantId,
        string email,
        string displayName,
        UserRole role,
        Guid? entraObjectId = null,
        bool isActive = true,
        DateTime? createdAtUtc = null,
        DateTime? lastLoginAtUtc = null)
    {
        using var db = new AppDbContext(_dbOptions, new FixedTenantContext(tenantId));
        var user = new AppUser(Guid.NewGuid(), tenantId, entraObjectId ?? Guid.NewGuid(), email, displayName, role,
            createdAtUtc ?? DateTime.UtcNow);
        if (lastLoginAtUtc is { } lastLogin)
        {
            user.RecordLogin(lastLogin);
        }
        if (!isActive)
        {
            user.Deactivate();
        }
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    /// <summary>Reads one tenant's audit log straight from the database, newest first.</summary>
    public List<AuditEvent> GetAuditEvents(Guid tenantId)
    {
        using var db = new AppDbContext(_dbOptions, new FixedTenantContext(tenantId));
        return db.AuditEvents.AsNoTracking().AsEnumerable().OrderByDescending(e => e.OccurredAtUtc).ToList();
    }

    /// <summary>Reads a tenant row (tenants are not tenant-owned).</summary>
    public Tenant GetTenant(Guid tenantId)
    {
        using var db = new AppDbContext(_dbOptions, new FixedTenantContext(null));
        return db.Tenants.AsNoTracking().Single(t => t.Id == tenantId);
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
