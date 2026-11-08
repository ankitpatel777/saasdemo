using SaasDemo.Application.Common;
using SaasDemo.Domain.Users;

namespace SaasDemo.Application.Users;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

public enum UserSortField
{
    DisplayName,
    Email,
    LastLogin,
    CreatedAt,
}

/// <param name="Search">Matches display name or email (contains, case-insensitive).</param>
/// <param name="IsActive">Null = both.</param>
public sealed record UserListQuery(
    string? Search,
    UserRole? Role,
    bool? IsActive,
    UserSortField Sort,
    bool Descending,
    int Page,
    int PageSize);

/// <summary>Reads users of the current tenant. Isolation is enforced by persistence, not by callers.</summary>
public interface IUserReadService
{
    Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct);
}
