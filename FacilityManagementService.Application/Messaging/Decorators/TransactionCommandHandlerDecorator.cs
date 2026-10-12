using FacilityManagementService.Application.Persistence;

namespace FacilityManagementService.Application.Messaging.Decorators;

/// <summary>
/// Runs the inner command handler in a unit of work, so its changes are saved and
/// committed together or not at all. Commands only; queries never write.
/// </summary>
internal sealed class TransactionCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IUnitOfWork unitOfWork)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(token => inner.HandleAsync(command, token), cancellationToken);
}
