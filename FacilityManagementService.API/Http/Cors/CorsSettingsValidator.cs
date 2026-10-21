using Microsoft.Extensions.Options;

namespace FacilityManagementService.API.Http.Cors;

/// <summary>
/// Checks <see cref="CorsSettings"/> at startup. A browser compares origins exactly, so an
/// origin with a path, a trailing slash or upper-case letters would silently never match.
/// </summary>
internal sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings settings)
    {
        var failures = settings.AllowedOrigins
            .Where(origin => !IsOrigin(origin))
            .Select(origin =>
                $"{CorsSettings.SectionName}:AllowedOrigins contains '{origin}', which is not an origin; " +
                "use the form https://app.example.com, in lower case, without a path or trailing slash.")
            .ToList();

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static bool IsOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.GetLeftPart(UriPartial.Authority) == origin;
}
