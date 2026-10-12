using FacilityManagementService.Application.Validation;

namespace FacilityManagementService.Application.Messaging.Decorators;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> for the command and throws
/// <see cref="ValidationException"/> without calling the inner handler if any report an error.
/// </summary>
internal sealed class ValidationCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        HandlerValidation.EnsureValid(command, validators);

        return inner.HandleAsync(command, cancellationToken);
    }
}
