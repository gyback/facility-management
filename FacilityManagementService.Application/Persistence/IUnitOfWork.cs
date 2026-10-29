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
    /// The operation runs on the same scoped handler and <c>DbContext</c> each time, so
    /// an implementation that retries must discard the failed attempt's tracked changes
    /// (<c>ChangeTracker.Clear()</c>) before re-running it; otherwise the retry tracks
    /// the same entities twice or inserts duplicates. Handlers must therefore keep all
    /// their writes in the <c>DbContext</c>, so that re-running them is safe.
    /// <para>
    /// Units of work do not nest: a command handler must not run another command. Share
    /// the logic through the domain or an application service instead, so that each
    /// command stays one use case in one transaction.
    /// </para>
    /// </remarks>
    /// <exception cref="ConflictException">
    /// A unique index rejected the changes. Not retried: the same changes would fail again.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Called while a unit of work is already running in the same scope.
    /// </exception>
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
