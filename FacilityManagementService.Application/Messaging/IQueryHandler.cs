namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// Handles a single query type. Endpoints inject this interface directly,
/// e.g. <c>IQueryHandler&lt;GetStay, StayDetails&gt;</c>.
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
