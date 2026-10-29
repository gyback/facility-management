using FacilityManagementService.API.Http.Idempotency;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FacilityManagementService.API.Http.OpenApi;

/// <summary>
/// Declares the required <c>Idempotency-Key</c> header on every <c>POST</c> operation, as
/// <see cref="IdempotencyKeyMiddleware"/> enforces it (ADR-0017).
/// </summary>
internal sealed class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (HttpMethods.IsPost(context.Description.HttpMethod ?? ""))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IdempotencyKeyMiddleware.HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "A new UUID per user action, reused for every retry of that action.",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MinLength = 1,
                    MaxLength = IdempotencyKeyMiddleware.MaxLength,
                    Pattern = IdempotencyKeyMiddleware.Pattern,
                },
            });
        }

        return Task.CompletedTask;
    }
}
