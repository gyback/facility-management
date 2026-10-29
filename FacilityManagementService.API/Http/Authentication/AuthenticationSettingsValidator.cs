using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.API.Http.Authentication;

/// <summary>
/// Checks <see cref="AuthenticationSettings"/> at startup, so a missing or malformed setting
/// fails before the host accepts requests.
/// </summary>
internal sealed partial class AuthenticationSettingsValidator : IValidateOptions<AuthenticationSettings>
{
    private const int MaxProviderLength = 32;

    public ValidateOptionsResult Validate(string? name, AuthenticationSettings settings)
    {
        var failures = new List<string>();
        const string section = AuthenticationSettings.SectionName;

        if (string.IsNullOrWhiteSpace(settings.Authority))
        {
            failures.Add($"{section}:Authority is required.");
        }
        else if (!IsHttpUri(settings.Authority))
        {
            failures.Add(
                $"{section}:Authority '{settings.Authority}' is not a valid HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            failures.Add($"{section}:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.Provider))
        {
            failures.Add($"{section}:Provider is required.");
        }
        else if (settings.Provider.Length > MaxProviderLength || !ProviderKey().IsMatch(settings.Provider))
        {
            failures.Add(
                $"{section}:Provider '{settings.Provider}' must be a short lower-case key such as 'entra', " +
                "not an issuer URL.");
        }

        if (string.IsNullOrWhiteSpace(settings.SubjectClaim))
        {
            failures.Add($"{section}:SubjectClaim is required, e.g. 'oid' for Entra or 'sub'.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsHttpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ProviderKey();
}

