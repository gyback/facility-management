using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.IntegrationTests.Probe;

public sealed class SqlServerHarnessTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    // SQL Server's error number for a duplicate key in a unique index.
    private const int DuplicateKeyInUniqueIndex = 2601;

    private TestDatabase<ProbeDbContext> _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await sqlServer.CreateDatabaseAsync<ProbeDbContext>(
            options => new(options),
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Migrations_are_applied_to_the_container()
    {
        await using var context = _database.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(cancellationToken));
    }

    [Fact]
    public async Task A_unique_index_rejects_a_duplicate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var context = _database.CreateContext())
        {
            context.Probes.Add(new ProbeEntity { Name = "Sauna" });
            await context.SaveChangesAsync(cancellationToken);
        }

        await using var duplicate = _database.CreateContext();
        duplicate.Probes.Add(new ProbeEntity { Name = "Sauna" });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            duplicate.SaveChangesAsync(cancellationToken));
        Assert.Equal(DuplicateKeyInUniqueIndex, Assert.IsType<SqlException>(exception.InnerException).Number);
    }

    [Fact]
    public async Task A_rolled_back_transaction_leaves_nothing_behind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var context = _database.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.Probes.Add(new ProbeEntity { Name = "Boathouse" });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }

        await using var assert = _database.CreateContext();
        Assert.False(await assert.Probes.AnyAsync(cancellationToken));
    }
}
