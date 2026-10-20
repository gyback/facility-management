using Microsoft.AspNetCore.Mvc;

namespace FacilityManagementService.API.Http.Problems;

/// <summary>
/// Writes a problem response through ASP.NET Core's <see cref="IProblemDetailsService"/>, so
/// every problem gets the same shape (ADR-0017). Nothing else builds an error body.
/// </summary>
internal static class ProblemWriter
{
    /// <param name="detail">English, for developers; must not quote user input.</param>
    /// <param name="extensions">Fields specific to the problem, e.g. a validation problem's <c>errors</c>.</param>
    /// <returns><see langword="false"/> when the client accepts no format the problem can be written in.</returns>
    public static ValueTask<bool> TryWriteAsync(
        HttpContext httpContext,
        int status,
        string code,
        string detail,
        IEnumerable<KeyValuePair<string, object?>>? extensions = null,
        Exception? exception = null)
    {
        var problem = new ProblemDetails { Status = status, Detail = detail };

        foreach (var (key, value) in extensions ?? [])
        {
            problem.Extensions[key] = value;
        }

        // After the extensions, so a field of the problem's own can never replace its code.
        problem.Extensions[ProblemCatalogue.CodeKey] = code;

        httpContext.Response.StatusCode = status;

        return httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception,
            });
    }
}
