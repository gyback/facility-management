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
    /// was committed. This is accepted until commands are made idempotent (#50).
    /// </remarks>
    public Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var attempted = false;

        return context.Database.CreateExecutionStrategy().ExecuteAsync(
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
}
