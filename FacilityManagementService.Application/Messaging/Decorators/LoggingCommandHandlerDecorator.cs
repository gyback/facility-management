using Microsoft.Extensions.Logging;

namespace FacilityManagementService.Application.Messaging.Decorators;

/// <summary>
/// Logs the start, duration and outcome of handling a command.
/// </summary>
internal sealed class LoggingCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggingCommandHandlerDecorator<TCommand, TResult>> logger)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default) =>
        HandlerLogging.LogAsync<TCommand, TResult>(logger, () => inner.HandleAsync(command, cancellationToken));
}
