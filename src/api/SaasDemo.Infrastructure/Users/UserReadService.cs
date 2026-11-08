using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Users;

/// <summary>No tenant WHERE clause here on purpose: the global query filter and RLS supply it.</summary>
public sealed class UserReadService(AppDbContext db) : IUserReadService
{
    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.Role })
            .ToListAsync(ct);

        return users.ConvertAll(u => new UserSummary(u.Id, u.Email, u.DisplayName, u.Role.ToString()));
    }
}
