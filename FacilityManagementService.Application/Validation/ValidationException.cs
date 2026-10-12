namespace FacilityManagementService.Application.Validation;

/// <summary>
/// Thrown by the validation decorator when a command or query fails validation.
/// The handler has not run.
/// </summary>
/// <remarks>
/// The message only states the number of errors: exception messages end up in logs, and
/// an error message may quote user input. The details are in <see cref="Errors"/>.
/// </remarks>
public sealed class ValidationException(IReadOnlyList<ValidationError> errors)
    : Exception($"Validation failed with {errors.Count} errors.")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
