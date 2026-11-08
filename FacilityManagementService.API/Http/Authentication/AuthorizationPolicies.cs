namespace FacilityManagementService.API.Http.Authentication;

/// <summary>
/// The API's authorisation policies (ADR-0015). The fallback policy requires a signed-in caller
/// whose identity is linked to a user; endpoints that an unlinked caller must be able to call
/// opt into <see cref="AuthenticatedIdentity"/> via <see cref="AllowUnregisteredUser{TBuilder}"/>.
/// </summary>
internal static class AuthorizationPolicies
{
    /// <summary>
    /// Requires a valid access token, without requiring that the identity is linked to a user.
    /// Used only for <c>AcceptInvitation</c> and reading one's own sign-in state (ADR-0015).
    /// </summary>
    public const string AuthenticatedIdentity = "AuthenticatedIdentity";

    /// <summary>
    /// Allows an authenticated caller whose identity is not yet linked to a user.
    /// </summary>
    public static TBuilder AllowUnregisteredUser<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(AuthenticatedIdentity);
}

