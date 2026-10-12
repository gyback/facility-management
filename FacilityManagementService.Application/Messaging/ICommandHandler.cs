namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// Handles a single command type. Endpoints inject this interface directly,
/// e.g. <c>ICommandHandler&lt;CreateStay, StayId&gt;</c>.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
