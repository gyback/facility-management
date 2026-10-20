using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FacilityManagementService.API.Http.Problems;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Rules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace FacilityManagementService.API.Tests.Http.Problems;

/// <summary>Every exception and bare status the API can meet, as the client receives it.</summary>
public sealed class ProblemResponseTests : IAsyncLifetime
{
    private const string CatalogueUrl =
        "https://github.com/gyback/facility-management/blob/master/FacilityManagementService.API/README.md";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(MapEndpoints);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/validation", () => Throw(new ValidationException(
        [
            ValidationError.Required("Body"),
            ValidationError.TooLong("Body", 4000),
            ValidationError.MustBeAfter("Guests[0].To", "From"),
        ])));
        app.MapGet("/rule", () => Throw(new NightsFullException()));
        app.MapGet("/conflict", () => Throw(new ConflictException(null, "IX_Things_Name", new InvalidOperationException())));
        app.MapGet("/rule-conflict", () => Throw(new ConflictException("nights-full", "IX_Nights", new InvalidOperationException())));
        app.MapGet("/boom", () => Throw(new InvalidOperationException("Boom.")));
        app.MapGet("/unauthorized", () => Results.Unauthorized());
        app.MapGet("/count", (int count) => count);
        app.MapPost("/things", (ThingRequest request) => request.Name);
    }

    [Fact]
    public async Task A_validation_failure_lists_each_propertys_errors_by_wire_name()
    {
        var problem = await GetProblemAsync("/validation", HttpStatusCode.BadRequest, "validation-failed");

        Assert.Equal("The request failed validation.", problem.GetProperty("title").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(["body", "guests[0].to"], errors.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            """[{"code":"required","message":"Is required.","parameters":{}},{"code":"too-long","message":"Must be at most 4000 characters.","parameters":{"maxLength":4000}}]""",
            errors.GetProperty("body").GetRawText());
        Assert.Equal(
            """[{"code":"must-be-after","message":"Must be after from.","parameters":{"otherProperty":"from"}}]""",
            errors.GetProperty("guests[0].to").GetRawText());
    }

    [Fact]
    public async Task A_domain_rule_refusal_is_a_conflict_with_the_rules_code_and_parameters()
    {
        var problem = await GetProblemAsync("/rule", HttpStatusCode.Conflict, "nights-full");

        Assert.Equal("The change conflicts with the current state.", problem.GetProperty("title").GetString());
        Assert.Equal("The nights are full.", problem.GetProperty("detail").GetString());
        Assert.Equal("""["2027-07-10"]""", problem.GetProperty("nights").GetRawText());
        Assert.Equal(12, problem.GetProperty("capacity").GetInt32());
    }

    [Fact]
    public async Task A_unique_index_without_a_rule_is_a_generic_conflict()
    {
        var problem = await GetProblemAsync("/conflict", HttpStatusCode.Conflict, "conflict");

        Assert.DoesNotContain("IX_Things_Name", problem.GetRawText());
    }

    [Fact]
    public async Task A_unique_index_with_a_rule_gives_the_rules_code()
    {
        await GetProblemAsync("/rule-conflict", HttpStatusCode.Conflict, "nights-full");
    }

    [Fact]
    public async Task An_unhandled_exception_hides_its_details_outside_Development_and_is_logged_once()
    {
        var problem = await GetProblemAsync("/boom", HttpStatusCode.InternalServerError, "internal-error");

        Assert.Equal("An unexpected error occurred.", problem.GetProperty("detail").GetString());
        var error = Assert.Single(_app.Services.GetFakeLogCollector().GetSnapshot(), log => log.Level >= LogLevel.Error);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    [Fact]
    public async Task An_unhandled_exception_shows_its_details_in_Development()
    {
        await using var app = await TestHost.StartAsync(MapEndpoints, environment: "Development");
        using var client = app.GetTestClient();

        var problem = await GetProblemAsync(client, "/boom", HttpStatusCode.InternalServerError, "internal-error");

        Assert.Contains("Boom.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_query_string_value_of_the_wrong_type_is_malformed_without_quoting_it()
    {
        var problem = await GetProblemAsync("/count?count=secret", HttpStatusCode.BadRequest, "malformed-request");

        Assert.DoesNotContain("secret", problem.GetRawText());
    }

    [Fact]
    public async Task A_body_that_cannot_be_parsed_is_malformed_without_quoting_it()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/things")
        {
            Content = new StringContent("""{"name": secret""", Encoding.UTF8, "application/json"),
            Headers = { { "Idempotency-Key", TestHost.IdempotencyKey } },
        };

        var problem = await SendForProblemAsync(_client, request, HttpStatusCode.BadRequest, "malformed-request");

        Assert.DoesNotContain("secret", problem.GetRawText());
    }

    [Fact]
    public async Task An_unmatched_route_is_not_found()
    {
        await GetProblemAsync("/nowhere", HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task A_bare_unauthorized_status_is_unauthenticated()
    {
        await GetProblemAsync("/unauthorized", HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task A_method_the_route_does_not_allow_is_method_not_allowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/things");

        await SendForProblemAsync(_client, request, HttpStatusCode.MethodNotAllowed, "method-not-allowed");
    }

    [Fact]
    public async Task A_request_the_client_aborted_gets_no_body()
    {
        var handler = new ApiExceptionHandler(
            new HostingEnvironment { EnvironmentName = Environments.Production },
            new FakeLogger<ApiExceptionHandler>());
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(canceled: true) };

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.HasStarted);
    }

    private Task<JsonElement> GetProblemAsync(string path, HttpStatusCode status, string code) =>
        GetProblemAsync(_client, path, status, code);

    private static Task<JsonElement> GetProblemAsync(HttpClient client, string path, HttpStatusCode status, string code) =>
        SendForProblemAsync(client, new HttpRequestMessage(HttpMethod.Get, path), status, code);

    /// <summary>Sends the request and checks the parts every problem has.</summary>
    internal static async Task<JsonElement> SendForProblemAsync(
        HttpClient client,
        HttpRequestMessage request,
        HttpStatusCode status,
        string code)
    {
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal($"{CatalogueUrl}#{code}", problem.GetProperty("type").GetString());
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));

        return problem;
    }

    private static IResult Throw(Exception exception) => throw exception;

    internal sealed record ThingRequest(string Name);

    private sealed class NightsFullException() : DomainRuleException(
        "nights-full",
        "The nights are full.",
        new Dictionary<string, object?> { ["nights"] = new[] { new DateOnly(2027, 7, 10) }, ["capacity"] = 12 });
}
