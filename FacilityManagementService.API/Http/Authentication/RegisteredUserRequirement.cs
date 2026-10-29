using FacilityManagementService.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace FacilityManagementService.API.Http.Authentication;

/// <summary>
/// Requires that the authenticated identity is linked to a user in our database (ADR-0015). Used
/// by the fallback authorisation policy on every endpoint unless it opts out with
/// <see cref="AuthorizationPolicies.AllowUnregisteredUser{TBuilder}"/> or <c>AllowAnonymous</c>.
/// </summary>
internal sealed class RegisteredUserRequirement : IAuthorizationRequirement;

internal sealed class RegisteredUserAuthorizationHandler(CurrentUser currentUser)
    : AuthorizationHandler<RegisteredUserRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RegisteredUserRequirement requirement)
    {
        if (currentUser.IsRegistered)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

