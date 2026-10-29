using System.Reflection;

namespace FacilityManagementService.API.Http.Problems;

/// <summary>
/// The codes of the problems the API itself produces (ADR-0017). Domain rules add their own
/// codes from <c>RuleCodes</c>. Each code has an entry in the catalogue in the API README,
/// which a test checks.
/// </summary>
internal static class ProblemCodes
{
    public const string MalformedRequest = "malformed-request";
    public const string ValidationFailed = "validation-failed";
    public const string IdempotencyKeyMissing = "idempotency-key-missing";
    public const string IdempotencyKeyInvalid = "idempotency-key-invalid";
    public const string Unauthenticated = "unauthenticated";
    public const string UserNotRegistered = "user-not-registered";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not-found";
    public const string MethodNotAllowed = "method-not-allowed";
    public const string Conflict = "conflict";
    public const string InternalError = "internal-error";

    /// <summary>Every code declared in this class.</summary>
    public static IReadOnlyCollection<string> All { get; } = typeof(ProblemCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
