using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SaasDemo.IntegrationTests;

/// <summary>What a validated Entra access token would say about the caller.</summary>
public sealed record TestUser(
    Guid TenantId,
    Guid ObjectId,
    string[] Roles,
    string? Scope = "access_as_user",
    string Name = "Test User",
    string Email = "test.user@example.com")
{
    public static TestUser For(Guid tenantId, params string[] roles) => new(tenantId, Guid.NewGuid(), roles);
}

/// <summary>
/// Stands in for JWT bearer validation: reads the claims from a test header. No header means
/// "no token", so the real challenge (401) path runs.
/// </summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(SchemeName + " ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var user = JsonSerializer.Deserialize<TestUser>(header[(SchemeName.Length + 1)..])!;
        var claims = new List<Claim>
        {
            new("tid", user.TenantId.ToString()),
            new("oid", user.ObjectId.ToString()),
            new("name", user.Name),
            new("preferred_username", user.Email),
        };
        claims.AddRange(user.Roles.Select(r => new Claim("roles", r)));
        if (user.Scope is not null)
        {
            claims.Add(new Claim("scp", user.Scope));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public static class TestClientExtensions
{
    /// <summary>A client whose requests carry <paramref name="user"/>'s claims, like a bearer token would.</summary>
    public static HttpClient CreateClientFor(this WebApplicationFactory<Program> factory, TestUser user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, JsonSerializer.Serialize(user));
        return client;
    }
}
