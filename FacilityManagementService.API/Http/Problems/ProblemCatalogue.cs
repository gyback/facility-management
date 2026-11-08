namespace FacilityManagementService.API.Http.Problems;

/// <summary>
/// The fixed parts of every problem: its <c>type</c> link and <c>title</c> by code, and the code
/// of a problem that has only a status, such as a bare <c>401</c> or an unmatched route.
/// </summary>
internal static class ProblemCatalogue
{
    /// <summary>The problem details extension that carries the code.</summary>
    public const string CodeKey = "code";

    private const string CatalogueUrl =
        "https://github.com/gyback/facility-management/blob/master/FacilityManagementService.API/README.md";

    /// <summary>The title of a refusal by a domain rule, whatever its code.</summary>
    private const string RuleTitle = "The change conflicts with the current state.";

    private static readonly Dictionary<string, string> TitleByCode = new()
    {
        [ProblemCodes.MalformedRequest] = "The request is malformed.",
        [ProblemCodes.ValidationFailed] = "The request failed validation.",
        [ProblemCodes.IdempotencyKeyMissing] = "The Idempotency-Key header is missing.",
        [ProblemCodes.IdempotencyKeyInvalid] = "The Idempotency-Key header is invalid.",
        [ProblemCodes.Unauthenticated] = "Authentication is required.",
        [ProblemCodes.UserNotRegistered] = "The signed-in identity is not registered.",
        [ProblemCodes.Forbidden] = "The caller is not allowed to do this.",
        [ProblemCodes.NotFound] = "The resource was not found.",
        [ProblemCodes.MethodNotAllowed] = "The method is not allowed for this resource.",
        [ProblemCodes.Conflict] = "The change conflicts with a change made in the meantime.",
        [ProblemCodes.InternalError] = "An unexpected error occurred.",
    };

    /// <summary>The link to the code's entry in the API README.</summary>
    public static string TypeFor(string code) => $"{CatalogueUrl}#{code}";

    /// <summary>The fixed title for the code; any code that is not the API's own is a domain rule's.</summary>
    public static string TitleFor(string code) => TitleByCode.GetValueOrDefault(code, RuleTitle);

    /// <summary>The code of a problem known only by its status.</summary>
    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => ProblemCodes.Unauthenticated,
        StatusCodes.Status403Forbidden => ProblemCodes.Forbidden,
        StatusCodes.Status404NotFound => ProblemCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => ProblemCodes.MethodNotAllowed,
        >= 500 => ProblemCodes.InternalError,
        _ => ProblemCodes.MalformedRequest,
    };

    /// <summary>
    /// Completes every problem the API writes, from the exception handler, status-code pages
    /// or the framework: sets <c>code</c> from the status if it has none, then <c>type</c> and
    /// <c>title</c> from the code. ASP.NET Core adds <c>traceId</c>.
    /// </summary>
    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        if (!problem.Extensions.TryGetValue(CodeKey, out var value) || value is not string code)
        {
            code = CodeForStatus(problem.Status ?? context.HttpContext.Response.StatusCode);
            problem.Extensions[CodeKey] = code;
        }

        problem.Type = TypeFor(code);
        problem.Title = TitleFor(code);
    }
}
