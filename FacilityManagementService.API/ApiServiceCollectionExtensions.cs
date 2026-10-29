using System.Text.Json;
using System.Text.Json.Serialization;
using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Http.Cors;
using FacilityManagementService.API.Http.Idempotency;
using FacilityManagementService.API.Http.OpenApi;
using FacilityManagementService.API.Http.Problems;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.API;

internal static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP conventions of ADR-0015 and ADR-0017: the wire format, problem details
    /// and the exception handler, JWT bearer authentication and the fallback authorisation
    /// policy, CORS and the OpenAPI document. Pair with
    /// <see cref="ApiApplicationBuilderExtensions.UseApi"/>.
    /// </summary>
    /// <remarks>
    /// Authentication and CORS are configured from the <c>Authentication</c> and <c>Cors</c>
    /// sections, validated when the host starts; see the API README.
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
        services.AddAuthenticationFromSettings();

        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<ProblemSchemaDocumentTransformer>();
            options.AddOperationTransformer<IdempotencyKeyOperationTransformer>();
            options.AddDocumentTransformer<ProblemSchemaDocumentTransformer>();
        });

        return services;
    }

    private static void AddAuthenticationFromSettings(this IServiceCollection services)
    {
        services.AddOptions<AuthenticationSettings>()
            .BindConfiguration(AuthenticationSettings.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AuthenticationSettings>, AuthenticationSettingsValidator>());

        // AddInfrastructure registers these too; TryAdd keeps TestHost working when it calls
        // AddApi() without AddInfrastructure().
        services.TryAddScoped<CurrentUser>();
        services.TryAddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthenticationSettings>>((jwt, settings) =>
            {
                var auth = settings.Value;

                // Keep the token's own claim names (oid, sub, name) rather than mapping them to
                // SOAP WS-Federation URIs (ADR-0015).
                jwt.MapInboundClaims = false;
                jwt.Authority = auth.Authority;
                jwt.Audience = auth.Audience;
                jwt.RequireHttpsMetadata =
                    Uri.TryCreate(auth.Authority, UriKind.Absolute, out var authority) &&
                    authority.Scheme == Uri.UriSchemeHttps;
                jwt.TokenValidationParameters.NameClaimType = auth.SubjectClaim;
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst(auth.SubjectClaim!)?.Value;

                        if (string.IsNullOrWhiteSpace(subject))
                        {
                            context.Fail($"The token has no '{auth.SubjectClaim}' claim.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        var authenticatedIdentity = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();

        var registeredUser = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new RegisteredUserRequirement())
            .Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(registeredUser)
            .SetFallbackPolicy(registeredUser)
            .AddPolicy(AuthorizationPolicies.AuthenticatedIdentity, authenticatedIdentity);

        services.AddScoped<IAuthorizationHandler, RegisteredUserAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationMiddlewareResultHandler>();
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
