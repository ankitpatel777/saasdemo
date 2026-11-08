using SaasDemo.Application.Audit;

namespace SaasDemo.Application.Users;

public enum UserStatusChangeOutcome
{
    Changed,
    Unchanged,
    NotFound,
    CannotChangeSelf,
}

/// <param name="User">The user after the call; null when <see cref="Outcome"/> is NotFound or CannotChangeSelf.</param>
public sealed record UserStatusChange(UserStatusChangeOutcome Outcome, UserListItem? User);

/// <summary>Activation is the only thing admins change about users: roles belong to Entra.</summary>
public interface IUserAdministrationService
{
    /// <summary>Idempotent: setting the current state again changes and audits nothing.</summary>
    Task<UserStatusChange> SetActiveAsync(Guid userId, bool active, AuditActor actor, CancellationToken ct);
}
