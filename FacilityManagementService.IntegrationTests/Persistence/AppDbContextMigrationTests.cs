using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.IntegrationTests.Persistence;

public sealed class AppDbContextMigrationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TestDatabase<AppDbContext> _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await sqlServer.CreateDatabaseAsync<AppDbContext>(
            options => new(options),
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Every_migration_applies_to_an_empty_database()
    {
        await using var context = _database.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        Assert.Equal(
            context.Database.GetMigrations(),
            await context.Database.GetAppliedMigrationsAsync(cancellationToken));
    }
}
