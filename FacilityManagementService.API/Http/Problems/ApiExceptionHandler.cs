using System.Text.Json;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Rules;
using Microsoft.AspNetCore.Diagnostics;

namespace FacilityManagementService.API.Http.Problems;

/// <summary>
/// Maps a closed list of exceptions to problem responses (ADR-0017); anything else is a
/// <c>500 internal-error</c>. No problem's <c>detail</c> quotes user input.
/// </summary>
/// <remarks>
/// Client errors are not logged here; the handler decorators already logged them. A
/// <c>500</c> is logged here, because the exception handler middleware does not log an
/// exception that a handler handled.
/// </remarks>
internal sealed partial class ApiExceptionHandler(IHostEnvironment environment, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client gave up; not an error, and nobody is left to read a body.
            LogRequestAborted();
            return true;
        }

        var (status, code, detail, extensions) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(exception);
        }

        return await ProblemWriter.TryWriteAsync(httpContext, status, code, detail, extensions, exception);
    }

    private Problem Map(Exception exception) => exception switch
    {
        // Its message quotes the value that failed to bind, so it is never sent.
        BadHttpRequestException badRequest => new(
            badRequest.StatusCode,
            ProblemCodes.MalformedRequest,
            "The request body could not be read, or a parameter has the wrong type."),
        ValidationException validation => new(
            StatusCodes.Status400BadRequest,
            ProblemCodes.ValidationFailed,
            "The request failed validation.",
            [new("errors", ToErrors(validation.Errors))]),
        DomainRuleException rule => new(
            StatusCodes.Status409Conflict,
            rule.Code,
            rule.Message,
            rule.Parameters),
        ConflictException conflict => new(
            StatusCodes.Status409Conflict,
            conflict.RuleCode ?? ProblemCodes.Conflict,
            "The change conflicts with a change made in the meantime."),
        _ => new(
            StatusCodes.Status500InternalServerError,
            ProblemCodes.InternalError,
            environment.IsDevelopment() ? exception.ToString() : "An unexpected error occurred."),
    };

    /// <summary>The errors grouped by property, keyed by its camelCase wire name.</summary>
    private static Dictionary<string, ValidationErrorResponse[]> ToErrors(IEnumerable<ValidationError> errors) =>
        errors
            .GroupBy(error => ToWireName(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(ValidationErrorResponse.From).ToArray());

    /// <summary>Camel-cases each segment of a property path, e.g. <c>Guests[0].Name</c> to <c>guests[0].name</c>.</summary>
    private static string ToWireName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    private readonly record struct Problem(
        int Status,
        string Code,
        string Detail,
        IEnumerable<KeyValuePair<string, object?>>? Extensions = null);

    [LoggerMessage(Level = LogLevel.Information, Message = "The client aborted the request")]
    private partial void LogRequestAborted();

    [LoggerMessage(Level = LogLevel.Error, Message = "The request failed with an unhandled exception")]
    private partial void LogUnhandled(Exception exception);
}
