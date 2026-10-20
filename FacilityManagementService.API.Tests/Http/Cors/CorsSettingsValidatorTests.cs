using FacilityManagementService.API.Http.Cors;

namespace FacilityManagementService.API.Tests.Http.Cors;

public sealed class CorsSettingsValidatorTests
{
    private readonly CorsSettingsValidator _validator = new();

    [Fact]
    public void No_origins_is_valid() =>
        Assert.True(_validator.Validate(null, new CorsSettings()).Succeeded);

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost:5173")]
    public void An_origin_is_valid(string origin) =>
        Assert.True(_validator.Validate(null, new CorsSettings { AllowedOrigins = [origin] }).Succeeded);

    [Theory]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/spa")]
    [InlineData("https://App.example.com")]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("")]
    public void Anything_but_an_exact_origin_is_rejected(string origin)
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = ["https://ok.example.com", origin] });

        var failure = Assert.Single(result.Failures!);
        Assert.StartsWith($"Cors:AllowedOrigins contains '{origin}', which is not an origin", failure);
    }
}
