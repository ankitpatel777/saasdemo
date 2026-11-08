using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaasDemo.Domain.Users;
using static SaasDemo.UnitTests.Persistence.SqliteTestDatabase;

namespace SaasDemo.UnitTests.Persistence;

public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static AppUser NewUser(Guid tenantId, string email) =>
        new(Guid.NewGuid(), tenantId, Guid.NewGuid(), email, email, UserRole.User, DateTime.UtcNow);

    private async Task AddUsersAsync(Guid tenantId, params string[] emails)
    {
        await using var db = _database.CreateContext(tenantId);
        db.Users.AddRange(emails.Select(e => NewUser(tenantId, e)));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task QueryFilter_ReturnsOnlyCurrentTenantsUsers()
    {
        await AddUsersAsync(TenantA, "a1@example.com", "a2@example.com");
        await AddUsersAsync(TenantB, "b1@example.com");

        await using var db = _database.CreateContext(TenantA);
        var emails = await db.Users.Select(u => u.Email).ToListAsync();

        emails.Should().BeEquivalentTo("a1@example.com", "a2@example.com");
    }

    [Fact]
    public async Task QueryFilter_ReturnsNothingWhenNoTenantIsResolved()
    {
        await AddUsersAsync(TenantA, "a1@example.com");

        await using var db = _database.CreateContext(tenantId: null);

        (await db.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SaveChanges_StampsCurrentTenantWhenTenantIdIsEmpty()
    {
        await using (var db = _database.CreateContext(TenantA))
        {
            db.Users.Add(NewUser(Guid.Empty, "a1@example.com"));
            await db.SaveChangesAsync();
        }

        await using var check = _database.CreateContext(TenantA);
        (await check.Users.SingleAsync()).TenantId.Should().Be(TenantA);
    }

    [Fact]
    public async Task SaveChanges_ThrowsOnCrossTenantInsert()
    {
        await using var db = _database.CreateContext(TenantA);
        db.Users.Add(NewUser(TenantB, "intruder@example.com"));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*another tenant*");
    }

    [Fact]
    public async Task SaveChanges_ThrowsWhenNoTenantIsResolved()
    {
        await using var db = _database.CreateContext(tenantId: null);
        db.Users.Add(NewUser(TenantA, "a1@example.com"));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no tenant is resolved*");
    }

    [Fact]
    public void SaveChanges_SyncOverload_IsGuardedToo()
    {
        using var db = _database.CreateContext(TenantA);
        db.Users.Add(NewUser(TenantB, "intruder@example.com"));

        var act = () => db.SaveChanges();

        act.Should().Throw<InvalidOperationException>();
    }
}
