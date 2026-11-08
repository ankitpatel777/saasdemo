using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Tenancy;
using SaasDemo.Domain.Tenants;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.UnitTests.Persistence;

/// <summary>Fixed tenant for tests; <c>null</c> means "no tenant resolved".</summary>
internal sealed class FakeTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid TenantId => tenantId ?? Guid.Empty;
    public string Slug => "test";
    public string Name => "Test";
    public Guid EntraTenantId => Guid.Empty;
    public bool IsResolved => tenantId.HasValue;
}

/// <summary>
/// One in-memory SQLite database per test, shared by every context it creates (the connection stays open).
/// SQLite exercises the EF query filter and SaveChanges guard; it has no Row-Level Security.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var db = CreateContext(tenantId: null);
        db.Database.EnsureCreated();
        db.Tenants.AddRange(
            new Tenant(TenantA, "tenant-a", "Tenant A", Guid.NewGuid(), true, DateTime.UtcNow),
            new Tenant(TenantB, "tenant-b", "Tenant B", Guid.NewGuid(), true, DateTime.UtcNow));
        db.SaveChanges();
    }

    public static Guid TenantA { get; } = Guid.NewGuid();
    public static Guid TenantB { get; } = Guid.NewGuid();

    public AppDbContext CreateContext(Guid? tenantId) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new FakeTenantContext(tenantId));

    public void Dispose() => _connection.Dispose();
}
