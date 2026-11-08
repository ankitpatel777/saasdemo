namespace SaasDemo.Application.Users;

public sealed record UserSummary(Guid Id, string Email, string DisplayName, string Role);

/// <summary>Reads users of the current tenant. Isolation is enforced by persistence, not by callers.</summary>
public interface IUserReadService
{
    Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct);
}
