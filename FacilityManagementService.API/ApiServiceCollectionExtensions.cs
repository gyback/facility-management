using System.Text.Json;
using System.Text.Json.Serialization;
using FacilityManagementService.API.Http.Cors;
using FacilityManagementService.API.Http.Idempotency;
using FacilityManagementService.API.Http.OpenApi;
using FacilityManagementService.API.Http.Problems;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.API;

internal static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP conventions of ADR-0017: the wire format, problem details and the
    /// exception handler, CORS and the OpenAPI document. Pair with
    /// <see cref="ApiApplicationBuilderExtensions.UseApi"/>.
    /// </summary>
    /// <remarks>
    /// CORS is configured from the <c>Cors</c> section, validated when the host starts; see the
    /// API README.
    /// </remarks>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // camelCase properties and nulls written are the web defaults already.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        // Binding failures reach the exception handler as malformed-request in every
        // environment, rather than as a bare 400 outside Development.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemCatalogue.Apply);
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddCorsFromSettings();

        // No policies until authentication is added (#41); UseAuthorization needs the services.
        services.AddAuthorization();

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<ProblemSchemaDocumentTransformer>();
            options.AddOperationTransformer<IdempotencyKeyOperationTransformer>();
        });

        return services;
    }

    private static void AddCorsFromSettings(this IServiceCollection services)
    {
        services.AddOptions<CorsSettings>()
            .BindConfiguration(CorsSettings.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>());

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((cors, settings) => cors.AddDefaultPolicy(policy => policy
                .WithOrigins(settings.Value.AllowedOrigins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
                .WithHeaders("Content-Type", "Authorization", IdempotencyKeyMiddleware.HeaderName)
                .WithExposedHeaders("Location", "Retry-After")));
    }
}
