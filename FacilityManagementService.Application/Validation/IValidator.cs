namespace FacilityManagementService.Application.Validation;

/// <summary>
/// Checks a command or query before its handler runs. Any number of validators may
/// exist per message; <c>AddApplication()</c> registers them and the validation
/// decorator runs them all.
/// </summary>
/// <typeparam name="T">
/// The command or query to validate. Validators run only for the exact message type, so
/// <typeparamref name="T"/> must be the concrete command or query, not an interface it implements.
/// </typeparam>
public interface IValidator<T>
{
    /// <returns>Every problem found; empty when <paramref name="instance"/> is valid.</returns>
    IEnumerable<ValidationError> Validate(T instance);
}
