namespace FacilityManagementService.API.Http.Cors;

/// <summary>
/// The origins allowed to call the API from a browser, bound from the <c>Cors</c> section
/// (e.g. <c>Cors__AllowedOrigins__0</c> in the environment). The SPA and the API are on
/// different origins (ADR-0006, ADR-0015).
/// </summary>
internal sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Origins such as <c>https://app.example.com</c>, without a path or trailing slash. Empty
    /// means no cross-origin access.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
