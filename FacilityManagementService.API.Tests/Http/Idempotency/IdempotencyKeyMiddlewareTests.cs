using System.Net;
using FacilityManagementService.API.Tests.Http.Problems;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace FacilityManagementService.API.Tests.Http.Idempotency;

public sealed class IdempotencyKeyMiddlewareTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(app =>
        {
            app.MapPost("/things", () => Results.Ok());
            app.MapPut("/things", () => Results.Ok());
            app.MapDelete("/things", () => Results.Ok());
            app.MapGet("/things", () => Results.Ok());
        });
        _client = _app.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task A_post_without_a_key_is_rejected()
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, Post(), HttpStatusCode.BadRequest, "idempotency-key-missing");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("åäö")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public async Task A_post_with_a_malformed_key_is_rejected(string key)
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, Post(key), HttpStatusCode.BadRequest, "idempotency-key-invalid");
    }

    [Fact]
    public async Task A_post_with_two_keys_is_rejected()
    {
        var request = Post();
        request.Headers.TryAddWithoutValidation("Idempotency-Key", ["first", "second"]);

        await ProblemResponseTests.SendForProblemAsync(
            _client, request, HttpStatusCode.BadRequest, "idempotency-key-invalid");
    }

    [Theory]
    [InlineData(TestHost.IdempotencyKey)]
    [InlineData("a")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234")]
    public async Task A_post_with_a_valid_key_reaches_the_endpoint(string key)
    {
        using var response = await _client.SendAsync(Post(key), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Other_methods_need_no_key(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/things");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_post_to_an_unknown_route_is_not_found_rather_than_missing_a_key()
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, new HttpRequestMessage(HttpMethod.Post, "/nowhere"), HttpStatusCode.NotFound, "not-found");
    }

    private static HttpRequestMessage Post(string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/things");

        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        return request;
    }
}
