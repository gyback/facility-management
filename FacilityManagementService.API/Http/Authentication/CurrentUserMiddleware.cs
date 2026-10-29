using FacilityManagementService.Application.Users;
using FacilityManagementService.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.API.Http.Authentication;

/// <summary>
/// Resolves the signed-in caller once per request, after authentication and before
/// authorisation (ADR-0015): pairs the configured subject claim with the configured provider
/// key, looks up the linked <see cref="Domain.Users.UserId"/> and stores both on the scoped
/// <see cref="CurrentUser"/>.
/// </summary>
internal sealed class CurrentUserMiddleware(
    RequestDelegate next,
    IOptions<AuthenticationSettings> settings)
{
    public const string DisplayNameClaim = "name";

    public async Task InvokeAsync(
        HttpContext context,
        CurrentUser currentUser,
        IUserIdentityLookup lookup)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null &&
            context.User.Identity is { IsAuthenticated: true })
        {
            var auth = settings.Value;
            var subject = context.User.FindFirst(auth.SubjectClaim!)?.Value;

            if (!string.IsNullOrWhiteSpace(subject))
            {
                var displayName = NullIfWhiteSpace(context.User.FindFirst(DisplayNameClaim)?.Value);
                var identity = new ExternalIdentity(auth.Provider!, subject, displayName);
                var userId = await lookup.FindUserIdAsync(identity.Provider, identity.Subject, context.RequestAborted);

                currentUser.Set(identity, userId);
            }
        }

        await next(context);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

