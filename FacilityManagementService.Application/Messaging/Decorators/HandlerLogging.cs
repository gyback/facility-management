using System.Diagnostics;
using FacilityManagementService.Application.Validation;
using Microsoft.Extensions.Logging;

namespace FacilityManagementService.Application.Messaging.Decorators;

/// <summary>
/// Shared by the logging decorators. Logs the message type and the outcome, never the
/// message itself, which may contain personal data.
/// </summary>
internal static partial class HandlerLogging
{
    public static async Task<TResult> LogAsync<TMessage, TResult>(
        ILogger logger,
        Func<Task<TResult>> handle,
        CancellationToken cancellationToken)
    {
        var messageName = typeof(TMessage).Name;
        var start = Stopwatch.GetTimestamp();

        logger.Handling(messageName);

        try
        {
            var result = await handle();
            logger.Handled(messageName, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return result;
        }
        catch (ValidationException exception)
        {
            logger.Rejected(messageName, Stopwatch.GetElapsedTime(start).TotalMilliseconds, exception.Errors.Count);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up, e.g. the client aborted the request; not a failure.
            logger.Cancelled(messageName, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.Failed(exception, messageName, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {MessageName}")]
    private static partial void Handling(this ILogger logger, string messageName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {MessageName} in {ElapsedMilliseconds} ms")]
    private static partial void Handled(this ILogger logger, string messageName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected {MessageName} after {ElapsedMilliseconds} ms with {ErrorCount} validation errors")]
    private static partial void Rejected(this ILogger logger, string messageName, double elapsedMilliseconds, int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cancelled {MessageName} after {ElapsedMilliseconds} ms")]
    private static partial void Cancelled(this ILogger logger, string messageName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handling {MessageName} failed after {ElapsedMilliseconds} ms")]
    private static partial void Failed(this ILogger logger, Exception exception, string messageName, double elapsedMilliseconds);
}
