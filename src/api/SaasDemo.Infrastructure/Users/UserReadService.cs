using Microsoft.EntityFrameworkCore;
using SaasDemo.Application.Common;
using SaasDemo.Application.Users;
using SaasDemo.Domain.Users;
using SaasDemo.Infrastructure.Persistence;

namespace SaasDemo.Infrastructure.Users;

/// <summary>No tenant WHERE clause here on purpose: the global query filter and RLS supply it.</summary>
public sealed class UserReadService(AppDbContext db) : IUserReadService
{
    private const string LikeEscape = "\\";

    public async Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // LIKE (not Contains) so matching is case-insensitive on both SQL Server and SQLite.
            // The user's wildcard characters are escaped, so they match literally.
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            users = users.Where(u =>
                EF.Functions.Like(u.DisplayName, pattern, LikeEscape) || EF.Functions.Like(u.Email, pattern, LikeEscape));
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        var total = await users.CountAsync(ct);

        var page = await Sort(users, query.Sort, query.Descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<UserListItem>(page.ConvertAll(ToListItem), query.Page, query.PageSize, total);
    }

    internal static UserListItem ToListItem(AppUser u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.IsActive, u.CreatedAtUtc, u.LastLoginAtUtc);

    /// <summary>Id is the tie-breaker, so pages stay stable when sort values repeat.</summary>
    private static IQueryable<AppUser> Sort(IQueryable<AppUser> users, UserSortField field, bool descending)
    {
        var sorted = (field, descending) switch
        {
            (UserSortField.Email, false) => users.OrderBy(u => u.Email),
            (UserSortField.Email, true) => users.OrderByDescending(u => u.Email),
            (UserSortField.LastLogin, false) => users.OrderBy(u => u.LastLoginAtUtc),
            (UserSortField.LastLogin, true) => users.OrderByDescending(u => u.LastLoginAtUtc),
            (UserSortField.CreatedAt, false) => users.OrderBy(u => u.CreatedAtUtc),
            (UserSortField.CreatedAt, true) => users.OrderByDescending(u => u.CreatedAtUtc),
            (_, false) => users.OrderBy(u => u.DisplayName),
            (_, true) => users.OrderByDescending(u => u.DisplayName),
        };
        return sorted.ThenBy(u => u.Id);
    }

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_")
            .Replace("[", LikeEscape + "[");
}
