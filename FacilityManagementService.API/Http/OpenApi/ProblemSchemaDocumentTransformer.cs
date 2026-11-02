using System.Text.Json.Nodes;
using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Http.Problems;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FacilityManagementService.API.Http.OpenApi;

/// <summary>
/// Describes every problem response with one <c>Problem</c> schema whose <c>code</c> is the
/// <c>ProblemCode</c> enum, and validation errors with the <c>ValidationErrorCode</c> enum, so
/// the generated client has a type for each set of codes (ADR-0017).
/// </summary>
/// <remarks>
/// Endpoints declare their problems with the built-in <c>.ProducesProblem(status)</c>; this
/// replaces the schema of each such response. It also declares the <c>401</c> and <c>403</c>
/// responses from authentication and the fallback policy (ADR-0015), the <c>400</c> every
/// <c>POST</c> can return for its idempotency key, and the <c>500</c> every operation can return.
/// </remarks>
internal sealed class ProblemSchemaDocumentTransformer
    : IOpenApiOperationTransformer, IOpenApiDocumentTransformer
{
    private const string ProblemContentType = "application/problem+json";

    private const string ProblemSchema = "Problem";
    private const string ProblemCodeSchema = "ProblemCode";
    private const string ValidationErrorSchema = "ValidationError";
    private const string ValidationErrorCodeSchema = "ValidationErrorCode";

    /// <summary>The framework's problem schemas, which <see cref="ProblemSchema"/> replaces.</summary>
    private static readonly string[] FrameworkProblemSchemas = ["ProblemDetails", "HttpValidationProblemDetails"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (!metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", ProblemResponse("Unauthorized"));

            var allowsUnregistered = metadata
                .OfType<IAuthorizeData>()
                .Any(data => data.Policy == AuthorizationPolicies.AuthenticatedIdentity);

            if (!allowsUnregistered)
            {
                operation.Responses.TryAdd("403", ProblemResponse("Forbidden"));
            }
        }

        return Task.CompletedTask;
    }

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        var schemas = document.Components.Schemas;

        schemas[ProblemCodeSchema] = StringEnum(ProblemCodes.All.Concat(RuleCodes.All));
        schemas[ValidationErrorCodeSchema] = StringEnum(ValidationCodes.All);
        schemas[ValidationErrorSchema] = ValidationError(document);
        schemas[ProblemSchema] = Problem(document);

        foreach (var schema in FrameworkProblemSchemas)
        {
            schemas.Remove(schema);
        }

        foreach (var (method, operation) in Operations(document))
        {
            operation.Responses ??= new OpenApiResponses();

            if (method == HttpMethod.Post)
            {
                operation.Responses.TryAdd("400", ProblemResponse("Bad Request"));
            }

            operation.Responses.TryAdd("500", ProblemResponse("Internal Server Error"));

            foreach (var response in operation.Responses.Values)
            {
                if (response.Content?.TryGetValue(ProblemContentType, out var mediaType) == true)
                {
                    mediaType.Schema = new OpenApiSchemaReference(ProblemSchema, document);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<(HttpMethod Method, OpenApiOperation Operation)> Operations(OpenApiDocument document) =>
        document.Paths?.Values
            .SelectMany(path => path.Operations ?? [])
            .Select(operation => (operation.Key, operation.Value))
        ?? [];

    private static OpenApiResponse ProblemResponse(string description) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType> { [ProblemContentType] = new() },
    };

    private static OpenApiSchema StringEnum(IEnumerable<string> values) => new()
    {
        Type = JsonSchemaType.String,
        Enum = values.Order(StringComparer.Ordinal).Select(value => (JsonNode)JsonValue.Create(value)).ToList(),
    };

    private static OpenApiSchema String() => new() { Type = JsonSchemaType.String };

    private static OpenApiSchema ValidationError(OpenApiDocument document) => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["code"] = new OpenApiSchemaReference(ValidationErrorCodeSchema, document),
            ["message"] = String(),
            ["parameters"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "The values the error's text needs, e.g. maxLength.",
            },
        },
        Required = new HashSet<string> { "code", "message", "parameters" },
    };

    private static OpenApiSchema Problem(OpenApiDocument document) => new()
    {
        Type = JsonSchemaType.Object,
        Description = "An RFC 9457 problem. Switch on code; never show title, detail or message to users.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = String(),
            ["title"] = String(),
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            ["detail"] = String(),
            ["instance"] = String(),
            ["code"] = new OpenApiSchemaReference(ProblemCodeSchema, document),
            ["traceId"] = String(),
            ["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "Present only when code is validation-failed: the errors of each property, by its camelCase name.",
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(ValidationErrorSchema, document),
                },
            },
        },
        Required = new HashSet<string> { "type", "title", "status", "code", "traceId" },
    };
}
