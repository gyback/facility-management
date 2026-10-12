using Microsoft.Extensions.Logging;

namespace FacilityManagementService.Application.Messaging.Decorators;

/// <summary>
/// Logs the start, duration and outcome of handling a query.
/// </summary>
internal sealed class LoggingQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> logger)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default) =>
        HandlerLogging.LogAsync<TQuery, TResult>(
            logger,
            () => inner.HandleAsync(query, cancellationToken),
            cancellationToken);
}
