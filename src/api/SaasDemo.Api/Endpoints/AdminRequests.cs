using FluentValidation;
using SaasDemo.Application.Users;
using SaasDemo.Domain.Tenants;
using SaasDemo.Domain.Users;

namespace SaasDemo.Api.Endpoints;

/// <summary>GET /users query string. Every value is optional; see <see cref="ToQuery"/> for the defaults.</summary>
public sealed record UsersQueryParameters(
    string? Search,
    string? Role,
    string? Status,
    string? Sort,
    string? Dir,
    int? Page,
    int? PageSize)
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public const int MaxSearchLength = 100;

    /// <summary>Call only after validation succeeded.</summary>
    public UserListQuery ToQuery() => new(
        string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Role is null ? null : Enum.Parse<UserRole>(Role, ignoreCase: true),
        Status?.ToLowerInvariant() switch { "active" => true, "inactive" => false, _ => null },
        Sort?.ToLowerInvariant() switch
        {
            "email" => UserSortField.Email,
            "lastlogin" => UserSortField.LastLogin,
            "createdat" => UserSortField.CreatedAt,
            _ => UserSortField.DisplayName,
        },
        string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase),
        Page ?? 1,
        PageSize ?? DefaultPageSize);
}

public sealed class UsersQueryParametersValidator : AbstractValidator<UsersQueryParameters>
{
    private static readonly string[] Roles = ["admin", "user"];
    private static readonly string[] Statuses = ["active", "inactive"];
    private static readonly string[] Sorts = ["displayname", "email", "lastlogin", "createdat"];
    private static readonly string[] Directions = ["asc", "desc"];

    public UsersQueryParametersValidator()
    {
        RuleFor(q => q.Search).MaximumLength(UsersQueryParameters.MaxSearchLength);
        RuleFor(q => q.Role).Must(BeOneOf(Roles)).WithMessage("Role must be Admin or User.");
        RuleFor(q => q.Status).Must(BeOneOf(Statuses)).WithMessage("Status must be active or inactive.");
        RuleFor(q => q.Sort).Must(BeOneOf(Sorts)).WithMessage("Sort must be displayName, email, lastLogin or createdAt.");
        RuleFor(q => q.Dir).Must(BeOneOf(Directions)).WithMessage("Dir must be asc or desc.");
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, UsersQueryParameters.MaxPageSize);
    }

    /// <summary>Null (not supplied) is allowed; otherwise case-insensitive membership.</summary>
    private static Func<string?, bool> BeOneOf(string[] allowed) =>
        value => value is null || allowed.Contains(value.ToLowerInvariant());
}

/// <summary>GET /audit query string.</summary>
public sealed record AuditQueryParameters(int? Page, int? PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
}

public sealed class AuditQueryParametersValidator : AbstractValidator<AuditQueryParameters>
{
    public AuditQueryParametersValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, AuditQueryParameters.MaxPageSize);
    }
}

/// <summary>PUT /settings body. Only the name is editable; the slug (the URL) never changes.</summary>
public sealed record UpdateSettingsRequest(string? Name);

public sealed record SettingsResponse(string Name);

public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(r => r.Name)
            .Must(name => name?.Trim().Length is >= Tenant.NameMinLength and <= Tenant.NameMaxLength)
            .WithMessage($"The organization name must be {Tenant.NameMinLength}–{Tenant.NameMaxLength} characters.");
    }
}
