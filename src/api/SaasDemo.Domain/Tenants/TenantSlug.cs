using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace SaasDemo.Domain.Tenants;

/// <summary>
/// Rules for tenant URL slugs: lowercase, 3–30 chars, [a-z0-9-], no leading/trailing '-'.
/// </summary>
public static partial class TenantSlug
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    /// <summary>Path segments that are never tenants (they belong to the platform).</summary>
    public static readonly FrozenSet<string> Reserved =
        FrozenSet.ToFrozenSet(["health", "platform", "admin", "api", "login"], StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? value) =>
        value is { Length: >= MinLength and <= MaxLength } && SlugPattern().IsMatch(value);

    public static bool IsReserved(string? value) => value is not null && Reserved.Contains(value);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
