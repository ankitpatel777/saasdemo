using SaasDemo.Domain.Tenants;

namespace SaasDemo.Api.Tenancy;

/// <summary>Decides whether a request path is tenant-scoped (/api/{tenant}/...) and extracts the slug.</summary>
public static class TenantRoutePath
{
    private static readonly PathString ApiPrefix = new("/api");

    public static bool TryGetSlug(PathString path, out string slug)
    {
        slug = string.Empty;

        if (!path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase, out var rest)
            || !rest.HasValue)
        {
            return false; // not under /api, or exactly "/api"
        }

        var value = rest.Value!.TrimStart('/');
        var end = value.IndexOf('/');
        var segment = end < 0 ? value : value[..end];

        // Platform routes such as /api/health are not tenants.
        if (segment.Length == 0 || TenantSlug.IsReserved(segment))
        {
            return false;
        }

        slug = segment;
        return true;
    }
}
