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
    /// retried as well, so in the rare case that the commit reached the database before the
    /// connection failed, the operation runs twice; unique indexes are the safety net.
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
