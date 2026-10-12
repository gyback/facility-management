using FacilityManagementService.Application.Validation;

namespace FacilityManagementService.Application.Messaging.Decorators;

internal static class HandlerValidation
{
    /// <exception cref="ValidationException">Any validator reports an error.</exception>
    public static void EnsureValid<TMessage>(TMessage message, IEnumerable<IValidator<TMessage>> validators)
    {
        var errors = validators.SelectMany(validator => validator.Validate(message)).ToList();

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
