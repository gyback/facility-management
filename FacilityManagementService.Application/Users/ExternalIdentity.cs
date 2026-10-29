namespace FacilityManagementService.Application.Users;

/// <summary>
/// The identity provider's identity behind the current request (ADR-0015). Used only when
/// linking an identity to a user; domain data references <see cref="Domain.Users.UserId"/>.
/// </summary>
/// <param name="Provider">The configured provider key, e.g. <c>entra</c>, not the token's <c>iss</c> claim.</param>
/// <param name="Subject">The stable subject claim from the token (<c>oid</c> for Entra, <c>sub</c> otherwise).</param>
/// <param name="DisplayName">The display name from the token, if it sent one.</param>
public sealed record ExternalIdentity(string Provider, string Subject, string? DisplayName);
