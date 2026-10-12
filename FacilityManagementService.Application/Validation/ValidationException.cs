namespace FacilityManagementService.Application.Validation;

/// <summary>
/// Thrown by the validation decorator when a command or query fails validation.
/// The handler has not run.
/// </summary>
public sealed class ValidationException(IReadOnlyList<ValidationError> errors)
    : Exception("Validation failed: " + string.Join("; ", errors.Select(error => $"{error.PropertyName}: {error.Message}")))
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
