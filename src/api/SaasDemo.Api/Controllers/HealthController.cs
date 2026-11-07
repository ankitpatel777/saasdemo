using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaasDemo.Api.Controllers;

public sealed record HealthResponse(string Status, string Version, DateTimeOffset TimestampUtc);

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController(TimeProvider timeProvider) : ControllerBase
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse("ok", Version, timeProvider.GetUtcNow()));
}
