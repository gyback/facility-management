using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.IntegrationTests;

/// <summary>
/// A migrated database created by <see cref="SqlServerFixture.CreateDatabaseAsync{TContext}"/>,
/// dropped when disposed.
/// </summary>
public sealed class TestDatabase<TContext>(
    DbContextOptions<TContext> options,
    Func<DbContextOptions<TContext>, TContext> createContext) : IAsyncDisposable
    where TContext : DbContext
{
    /// <summary>
    /// Creates a new context, with its own connection and change tracker, over the database.
    /// Use a separate context to act and to assert, as separate requests would.
    /// </summary>
    public TContext CreateContext() => createContext(options);

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
