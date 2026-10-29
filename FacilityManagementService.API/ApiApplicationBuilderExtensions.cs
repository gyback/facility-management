using FacilityManagementService.API.Http.Idempotency;

namespace FacilityManagementService.API;

internal static class ApiApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the request pipeline of ADR-0017 and maps the OpenAPI document. Feature endpoints
    /// are mapped after this.
    /// </summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        // Outermost, so every exception below becomes a problem; status-code pages turn a bare
        // error status, such as an unmatched route, into one too.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors();
        app.UseAuthorization();

        // After routing, so it knows whether the request matched a POST endpoint.
        app.UseMiddleware<IdempotencyKeyMiddleware>();

        // In every environment and anonymously (ADR-0015): the client generator reads it.
        app.MapOpenApi().AllowAnonymous();

        return app;
    }
}
