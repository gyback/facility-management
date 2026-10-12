namespace FacilityManagementService.Application.Persistence;

/// <summary>
/// The transaction boundary around a command, applied by the transaction decorator.
/// Implemented in Infrastructure.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction, saves the changes it made and
    /// commits. If <paramref name="operation"/> throws, nothing is saved and the
    /// transaction is rolled back.
    /// </summary>
    /// <remarks>
    /// The operation is a delegate so the implementation can re-run it as a whole,
    /// e.g. under an EF Core execution strategy that retries transient failures.
    /// </remarks>
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
