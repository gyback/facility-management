using FacilityManagementService.API.Http.Problems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FacilityManagementService.API.Http.Authentication;

/// <summary>
/// Writes a <c>403 user-not-registered</c> problem when the fallback policy rejects a signed-in
/// identity that is not linked to a user (ADR-0015, ADR-0017), rather than the generic
/// <c>forbidden</c> problem a bare <c>403</c> status produces.
/// </summary>
internal sealed class ProblemAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden &&
            authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<RegisteredUserRequirement>()
                .Any() == true)
        {
            await ProblemWriter.TryWriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                ProblemCodes.UserNotRegistered,
                "The signed-in identity is not linked to a user.");
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}

