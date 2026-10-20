using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace FacilityManagementService.API.Tests.Http.Cors;

public sealed class CorsTests : IAsyncLifetime
{
    private const string SpaOrigin = "https://app.example.com";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(
            app => app.MapGet("/things", () => Results.Created("/things/1", null)),
            settings: new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = SpaOrigin });
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task A_preflight_from_the_spa_allows_the_idempotency_key_header()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/things")
        {
            Headers =
            {
                { "Origin", SpaOrigin },
                { "Access-Control-Request-Method", "POST" },
                { "Access-Control-Request-Headers", "content-type,idempotency-key" },
            },
        };

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(SpaOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        var allowed = string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("idempotency-key", allowed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_response_to_the_spa_exposes_location_and_retry_after()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/things") { Headers = { { "Origin", SpaOrigin } } };

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var exposed = string.Join(',', response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains("Location", exposed);
        Assert.Contains("Retry-After", exposed);
    }

    [Fact]
    public async Task Another_origin_is_not_allowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/things") { Headers = { { "Origin", "https://evil.example.com" } } };

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
