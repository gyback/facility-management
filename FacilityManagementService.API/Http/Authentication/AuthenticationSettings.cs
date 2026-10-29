namespace FacilityManagementService.API.Http.Authentication;

/// <summary>
/// How the API validates bearer access tokens and identifies the provider behind them, bound
/// from the <c>Authentication</c> configuration section (e.g. <c>Authentication__Authority</c>
/// in the environment; ADR-0007, ADR-0015).
/// </summary>
internal sealed class AuthenticationSettings
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// The OpenID Connect authority that issues tokens, e.g. the Entra External ID tenant URL in
    /// production or the mock OIDC server in local development and staging.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>The expected <c>aud</c> claim of access tokens for this API.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// A short, stable key for the identity provider, e.g. <c>entra</c>, stored with linked
    /// identities instead of the token's <c>iss</c> claim (ADR-0015).
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// The token claim that carries the provider's stable user identifier: <c>oid</c> for Entra
    /// External ID, <c>sub</c> for other OpenID Connect providers.
    /// </summary>
    public string? SubjectClaim { get; set; }
}

