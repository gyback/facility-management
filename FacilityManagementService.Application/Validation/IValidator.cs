namespace FacilityManagementService.Application.Validation;

/// <summary>
/// Checks a command or query before its handler runs. Any number of validators may
/// exist per message; <c>AddApplication()</c> registers them and the validation
/// decorator runs them all.
/// </summary>
/// <typeparam name="T">The command or query to validate.</typeparam>
public interface IValidator<in T>
{
    /// <returns>Every problem found; empty when <paramref name="instance"/> is valid.</returns>
    IEnumerable<ValidationError> Validate(T instance);
}
