using FacilityManagementService.IntegrationTests;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace FacilityManagementService.IntegrationTests;

/// <summary>
/// One SQL Server container shared by every integration test in the assembly. Tests run
/// against the real engine rather than the in-memory provider or SQLite, so unique
/// indexes, foreign keys and transactions behave as they do in production (ADR-0008).
/// Requires a running Docker daemon.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // SQL Server 2022 is the engine behind Azure SQL Database and the homelab's
    // SQL Server Express (ADR-0008).
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>
    /// Creates an empty database with a unique name and applies <typeparamref name="TContext"/>'s
    /// migrations to it. Call it from a test class's <c>InitializeAsync</c>, which xUnit
    /// runs once per test, so every test gets its own schema and data.
    /// </summary>
    /// <param name="createContext">Creates a context from the options the database is reached with.</param>
    public async Task<TestDatabase<TContext>> CreateDatabaseAsync<TContext>(
        Func<DbContextOptions<TContext>, TContext> createContext,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"test_{Guid.NewGuid():N}",
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString)
            .Options;

        var database = new TestDatabase<TContext>(options, createContext);

        await using var context = database.CreateContext();

        // The same call the migration entry point (#11) makes: it creates the database
        // if it does not exist and applies every pending migration, never EnsureCreated.
        await context.Database.MigrateAsync(cancellationToken);

        return database;
    }
}
