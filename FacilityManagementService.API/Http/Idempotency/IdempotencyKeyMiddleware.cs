using System.Text.RegularExpressions;
using FacilityManagementService.API.Http.Problems;

namespace FacilityManagementService.API.Http.Idempotency;

/// <summary>
/// Requires an <c>Idempotency-Key</c> header on every <c>POST</c> to an endpoint (ADR-0017).
/// Only checks that the key is present and well formed; storing keys and replaying responses
/// is #50.
/// </summary>
internal sealed partial class IdempotencyKeyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "Idempotency-Key";

    public const int MaxLength = 64;

    /// <summary>1 to 64 visible ASCII characters; the SPA sends a UUID.</summary>
    public const string Pattern = @"^[\x21-\x7E]{1,64}$";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !AcceptsPost(context.GetEndpoint()))
        {
            await next(context);
            return;
        }

        var values = context.Request.Headers[HeaderName];

        if (values.Count == 0)
        {
            await ProblemWriter.TryWriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                ProblemCodes.IdempotencyKeyMissing,
                $"Every POST request needs an {HeaderName} header.");
            return;
        }

        if (values.Count > 1 || !KeyPattern().IsMatch(values[0] ?? ""))
        {
            await ProblemWriter.TryWriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                ProblemCodes.IdempotencyKeyInvalid,
                $"The {HeaderName} header must be a single value of 1 to {MaxLength} visible ASCII characters.");
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Whether routing matched an endpoint that handles <c>POST</c>. An unmatched route, or a
    /// method the endpoint does not allow, is answered by routing instead.
    /// </summary>
    private static bool AcceptsPost(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Post) == true;

    [GeneratedRegex(Pattern)]
    private static partial Regex KeyPattern();
}
