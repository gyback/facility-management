using System.Net.Http.Json;
using System.Text.Json;
using FacilityManagementService.API.Http.Problems;
using FacilityManagementService.API.Http.Responses;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Rules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;

namespace FacilityManagementService.API.Tests.Http.OpenApi;

/// <summary>The OpenAPI document as the client generator (#17) reads it.</summary>
public sealed class OpenApiDocumentTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private JsonElement _document;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(app =>
        {
            app.MapPost("/things", CreateThing).WithName("CreateThing").ProducesProblem(StatusCodes.Status409Conflict);
            app.MapGet("/things/{thingId:guid}", (Guid thingId) => TypedResults.Ok(thingId.ToString())).WithName("GetThing");
        });

        using var client = _app.GetTestClient();
        _document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private static Created<CreatedResponse> CreateThing()
    {
        var id = ThingId.New();
        return TypedResults.Created($"/things/{id.Value}", CreatedResponse.From(id));
    }

    private JsonElement Schemas => _document.GetProperty("components").GetProperty("schemas");

    private JsonElement Operation(string path, string method) =>
        _document.GetProperty("paths").GetProperty(path).GetProperty(method);

    [Fact]
    public void Problem_codes_are_an_enum_of_the_apis_and_the_rules_codes() =>
        Assert.Equal(
            ProblemCodes.All.Concat(RuleCodes.All).Order(StringComparer.Ordinal),
            Enum(Schemas.GetProperty("ProblemCode")));

    [Fact]
    public void Validation_codes_are_an_enum() =>
        Assert.Equal(ValidationCodes.All.Order(StringComparer.Ordinal), Enum(Schemas.GetProperty("ValidationErrorCode")));

    [Fact]
    public void The_problem_schema_has_a_code_and_a_trace_id()
    {
        var problem = Schemas.GetProperty("Problem");

        Assert.Equal("#/components/schemas/ProblemCode", problem.GetProperty("properties").GetProperty("code").GetProperty("$ref").GetString());
        Assert.Equal(
            ["code", "status", "title", "traceId", "type"],
            problem.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Order());
        Assert.Equal(
            "#/components/schemas/ValidationError",
            problem.GetProperty("properties").GetProperty("errors").GetProperty("additionalProperties")
                .GetProperty("items").GetProperty("$ref").GetString());
    }

    [Fact]
    public void The_frameworks_problem_schemas_are_replaced() =>
        Assert.False(Schemas.TryGetProperty("ProblemDetails", out _));

    [Theory]
    [InlineData("400")]
    [InlineData("409")]
    [InlineData("500")]
    public void A_posts_problem_responses_use_the_problem_schema(string status) =>
        Assert.Equal("#/components/schemas/Problem", ProblemSchemaRef(Operation("/things", "post"), status));

    [Fact]
    public void A_get_declares_the_500_but_not_the_idempotency_400()
    {
        var operation = Operation("/things/{thingId}", "get");

        Assert.Equal("#/components/schemas/Problem", ProblemSchemaRef(operation, "500"));
        Assert.False(operation.GetProperty("responses").TryGetProperty("400", out _));
        Assert.DoesNotContain(Parameters(operation), parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");
    }

    [Fact]
    public void A_post_requires_the_idempotency_key_header()
    {
        var header = Assert.Single(
            Parameters(Operation("/things", "post")),
            parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");

        Assert.Equal("header", header.GetProperty("in").GetString());
        Assert.True(header.GetProperty("required").GetBoolean());
        Assert.Equal(64, header.GetProperty("schema").GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public void The_operation_id_is_the_endpoints_name() =>
        Assert.Equal("CreateThing", Operation("/things", "post").GetProperty("operationId").GetString());

    [Fact]
    public void The_created_response_requires_an_id_and_warnings() =>
        Assert.Equal(
            ["id", "warnings"],
            Schemas.GetProperty(nameof(CreatedResponse)).GetProperty("required").EnumerateArray().Select(name => name.GetString()).Order());

    [Fact]
    public void The_created_responses_id_is_a_uuid_string()
    {
        var id = Schemas.GetProperty(nameof(CreatedResponse)).GetProperty("properties").GetProperty("id");

        Assert.Equal("string", id.GetProperty("type").GetString());
        Assert.Equal("uuid", id.GetProperty("format").GetString());
    }

    private static IEnumerable<string?> Enum(JsonElement schema) =>
        schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString());

    private static IEnumerable<JsonElement> Parameters(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters) ? parameters.EnumerateArray().ToList() : [];

    private static string? ProblemSchemaRef(JsonElement operation, string status) =>
        operation.GetProperty("responses").GetProperty(status).GetProperty("content")
            .GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref").GetString();
}
