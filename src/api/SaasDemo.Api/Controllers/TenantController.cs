using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaasDemo.Application.Tenancy;

namespace SaasDemo.Api.Controllers;

public sealed record TenantInfoResponse(Guid TenantId, string Slug, string Name);

[ApiController]
[Route("api/{tenant}")]
// TODO(step 7.4): replace with [Authorize] once the JWT 'tid' check against ITenantContext is in place.
[AllowAnonymous]
public sealed class TenantController(ITenantContext tenantContext) : ControllerBase
{
    [HttpGet("info")]
    [ProducesResponseType<TenantInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<TenantInfoResponse> GetInfo()
    {
        // Defence in depth: e.g. /api/admin/info is reserved, so the middleware resolves nothing.
        if (!tenantContext.IsResolved)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found");
        }

        return Ok(new TenantInfoResponse(tenantContext.TenantId, tenantContext.Slug, tenantContext.Name));
    }
}
