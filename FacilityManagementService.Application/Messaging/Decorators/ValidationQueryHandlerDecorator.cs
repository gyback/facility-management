using FacilityManagementService.Application.Validation;

namespace FacilityManagementService.Application.Messaging.Decorators;

/// <summary>
/// Runs every <see cref="IValidator{T}"/> for the query and throws
/// <see cref="ValidationException"/> without calling the inner handler if any report an error.
/// </summary>
internal sealed class ValidationQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        HandlerValidation.EnsureValid(query, validators);

        return inner.HandleAsync(query, cancellationToken);
    }
}
