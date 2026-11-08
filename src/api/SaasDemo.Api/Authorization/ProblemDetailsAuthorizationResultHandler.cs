using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace SaasDemo.Api.Authorization;

/// <summary>
/// Turns authorization failures into ProblemDetails. 403s carry the first failure reason from our
/// handlers (e.g. "No role assigned in your organization"). 401s go through the normal challenge,
/// which keeps the WWW-Authenticate header; UseStatusCodePages then writes the ProblemDetails body.
/// </summary>
public sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var detail = authorizeResult.AuthorizationFailure?.FailureReasons.FirstOrDefault()?.Message
            ?? "You do not have access to this resource.";

        await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail)
            .ExecuteAsync(context);
    }
}
