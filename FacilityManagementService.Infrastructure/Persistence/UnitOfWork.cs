using FacilityManagementService.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Runs a command in one transaction under the EF Core execution strategy, and is the
/// only caller of <c>SaveChanges</c>.
/// </summary>
internal sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    /// <inheritdoc />
    /// <remarks>
    /// The execution strategy re-runs the whole attempt on a transient failure, after
    /// discarding the changes the failed attempt tracked. A failure while committing is
    /// retried as well, without checking whether the commit reached the database. In the rare
    /// case that it did, the operation runs a second time: its changes are saved twice, or,
    /// when a unique index rejects the duplicate, the command fails although its first attempt
    /// was committed (a <see cref="ConflictException"/>). This is accepted until commands are made
    /// idempotent (#50).
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

        var attempted = false;

        try
        {
            return await context.Database.CreateExecutionStrategy().ExecuteAsync(
                async token =>
                {
                    if (attempted)
                    {
                        context.ChangeTracker.Clear();
                    }

                    attempted = true;

                    await using var transaction = await context.Database.BeginTransactionAsync(token);
                    var result = await operation(token);
                    await context.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);

                    return result;
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueIndexViolation.TryGetIndexName(exception, out var indexName))
        {
            // Caught outside the strategy, so the strategy still sees the original error and
            // decides not to retry it.
            throw new ConflictException(UniqueIndexRules.FindRuleCode(indexName), indexName, exception);
        }
    }
}
