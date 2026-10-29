using FacilityManagementService.API.Http.Authentication;

namespace FacilityManagementService.API.Tests.Http.Authentication;

public sealed class AuthenticationSettingsValidatorTests
{
    private readonly AuthenticationSettingsValidator _validator = new();

    [Theory]
    [InlineData("https://tenant.ciamlogin.com/00000000-0000-0000-0000-000000000000/v2.0", "entra", "oid")]
    [InlineData("http://localhost:8080/default", "mock", "sub")]
    [InlineData("https://auth.example.com", "custom-oidc-2", "sub")]
    public void Complete_settings_are_valid(string authority, string provider, string subjectClaim)
    {
        var result = _validator.Validate(null, ValidSettings(authority, provider, subjectClaim));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Missing_settings_report_every_required_key()
    {
        var result = _validator.Validate(null, new AuthenticationSettings());

        Assert.Collection(
            result.Failures!,
            failure => Assert.Equal("Authentication:Authority is required.", failure),
            failure => Assert.Equal("Authentication:Audience is required.", failure),
            failure => Assert.Equal("Authentication:Provider is required.", failure),
            failure => Assert.StartsWith("Authentication:SubjectClaim is required", failure));
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://auth.example.com")]
    [InlineData("/relative")]
    public void An_authority_that_is_not_an_http_uri_is_rejected(string authority)
    {
        var settings = ValidSettings(authority: authority);

        var result = _validator.Validate(null, settings);

        var failure = Assert.Single(result.Failures!);
        Assert.StartsWith($"Authentication:Authority '{authority}' is not a valid HTTP or HTTPS URI", failure);
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/tenant/v2.0")]
    [InlineData("Entra")]
    [InlineData("entra_id")]
    [InlineData("-entra")]
    [InlineData("123456789012345678901234567890123")]
    public void A_provider_that_is_not_a_short_lowercase_key_is_rejected(string provider)
    {
        var settings = ValidSettings(provider: provider);

        var result = _validator.Validate(null, settings);

        var failure = Assert.Single(result.Failures!);
        Assert.StartsWith($"Authentication:Provider '{provider}' must be a short lower-case key", failure);
    }

    private static AuthenticationSettings ValidSettings(
        string authority = "https://auth.example.com",
        string provider = "entra",
        string subjectClaim = "oid") => new()
    {
        Authority = authority,
        Audience = "facility-management-api",
        Provider = provider,
        SubjectClaim = subjectClaim,
    };
}

