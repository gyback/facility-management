using FacilityManagementService.Application.Validation;

namespace FacilityManagementService.API.Http.Problems;

/// <summary>One error in a <c>validation-failed</c> problem's <c>errors</c>.</summary>
/// <param name="Code">A <see cref="ValidationCodes"/> constant; the frontend translates it.</param>
/// <param name="Message">English, for developers.</param>
/// <param name="Parameters">The values the error's text needs; empty when there are none.</param>
internal sealed record ValidationErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object> Parameters)
{
    private static readonly IReadOnlyDictionary<string, object> NoParameters = new Dictionary<string, object>();

    public static ValidationErrorResponse From(ValidationError error) =>
        new(error.Code, error.Message, error.Parameters ?? NoParameters);
}
