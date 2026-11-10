using FacilityManagementService.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Runs a command in one transaction under the EF Core execution strategy, and is the
/// only caller of <c>SaveChanges</c>.
/// </summary>
internal sealed class UnitOfWork(
    AppDbContext context,
    IdempotencyContext idempotencyContext,
    TimeProvider timeProvider) : IUnitOfWork
{
    /// <inheritdoc />
    /// <remarks>
    /// When <see cref="IdempotencyContext"/> holds a replayed result for a previously committed
    /// <c>Idempotency-Key</c>, <see cref="ExecuteAsync{TResult}"/> returns the deserialised result
    /// without opening a transaction or calling <paramref name="operation"/> (ADR-0017, ADR-0019).
    /// <para>
    /// Otherwise, each attempt deletes expired <see cref="CommandMarker"/> rows, runs
    /// <paramref name="operation"/>, stages a <see cref="CommandMarker"/> in the same transaction,
    /// saves and commits. If a transient failure occurs during <c>CommitAsync</c>, the execution
    /// strategy calls <c>verifySucceeded</c>, which checks whether the marker was committed and,
    /// if so, returns the first attempt's result without re-running <paramref name="operation"/>
    /// (ADR-0019).
    /// </para>
    /// <para>
    /// A unique index rejecting the changes (SQL Server error 2627 or 2601) is not transient, so
    /// the execution strategy does not retry it; it is translated to a
    /// <see cref="ConflictException"/> carrying the rule code from <see cref="UniqueIndexRules"/>.
    /// </para>
    /// </remarks>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "A unit of work is already running in this scope; a command cannot run another command. " +
                "Share the logic through the domain or an application service instead.");
        }

        if (idempotencyContext.TryGetReplayedResult<TResult>(out var replayedResult))
        {
            return replayedResult;
        }

        var state = new CommitState<TResult>();

        try
        {
            return await context.Database.CreateExecutionStrategy().ExecuteAsync(
                state,
                async (dbContext, attempt, token) =>
                {
                    if (attempt.Attempted)
                    {
                        dbContext.ChangeTracker.Clear();
                    }

                    attempt.Attempted = true;
                    attempt.CommitAttempted = false;

                    await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
                    var result = await operation(token);

                    var now = timeProvider.GetUtcNow();
                    await dbContext.Set<CommandMarker>()
                        .Where(marker => marker.ExpiresAt <= now)
                        .ExecuteDeleteAsync(token);

                    var marker = CreateMarker(result, now);
                    dbContext.Set<CommandMarker>().Add(marker);

                    await dbContext.SaveChangesAsync(token);

                    attempt.Scope = marker.Scope;
                    attempt.Key = marker.Key;
                    attempt.Result = result;
                    attempt.CommitAttempted = true;

                    await transaction.CommitAsync(token);

                    return result;
                },
                static async (dbContext, attempt, token) =>
                {
                    if (!attempt.CommitAttempted || attempt.Scope is null || attempt.Key is null)
                    {
                        return new ExecutionResult<TResult>(false, default!);
                    }

                    attempt.CommitAttempted = false;
                    dbContext.ChangeTracker.Clear();

                    var committed = await dbContext.Set<CommandMarker>()
                        .AsNoTracking()
                        .AnyAsync(
                            marker => marker.Scope == attempt.Scope && marker.Key == attempt.Key,
                            token);

                    return committed
                        ? new ExecutionResult<TResult>(true, attempt.Result!)
                        : new ExecutionResult<TResult>(false, default!);
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException(null, "concurrency", exception);
        }
        catch (DbUpdateException exception) when (UniqueIndexViolation.TryGetIndexName(exception, out var indexName))
        {
            // Caught outside the strategy, so the strategy still sees the original error and
            // decides not to retry it.
            throw new ConflictException(UniqueIndexRules.FindRuleCode(indexName), indexName, exception);
        }
    }

    private CommandMarker CreateMarker<TResult>(TResult result, DateTimeOffset now)
    {
        if (idempotencyContext.Request is { } request)
        {
            return CommandMarker.ForIdempotencyKey(
                request.Scope,
                request.Key,
                request.Fingerprint,
                CommandResultSerializer.Serialize(result),
                now);
        }

        return CommandMarker.ForCommitVerification(Guid.CreateVersion7().ToString(), now);
    }

    private sealed class CommitState<TResult>
    {
        public bool Attempted { get; set; }

        public bool CommitAttempted { get; set; }

        public string? Scope { get; set; }

        public string? Key { get; set; }

        public TResult? Result { get; set; }
    }
}
