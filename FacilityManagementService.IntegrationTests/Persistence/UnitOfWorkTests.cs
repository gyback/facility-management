using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Infrastructure;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityManagementService.IntegrationTests.Persistence;

/// <summary>
/// <see cref="UnitOfWork"/> as <c>AddInfrastructure</c> registers it, against the migrated
/// application database. A <see cref="Note"/> table, added to the model for these tests only,
/// stands in for an aggregate.
/// </summary>
public sealed class UnitOfWorkTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    // User-defined error numbers the tests raise with THROW; only the first one is treated as
    // transient, standing in for errors such as a deadlock (1205).
    private const int SimulatedTransientError = 50001;
    private const int SimulatedPermanentError = 50002;

    private TestDatabase<AppDbContext> _database = null!;
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _database = await sqlServer.CreateDatabaseAsync<AppDbContext>(options => new(options), cancellationToken);

        await using var context = _database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE Notes (Id uniqueidentifier NOT NULL PRIMARY KEY, Text nvarchar(100) NOT NULL)",
            cancellationToken);

        _services = AddInfrastructure(context.Database.GetConnectionString()!);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task The_operations_changes_are_committed_and_its_result_returned()
    {
        var result = await ExecuteAsync((context, _) =>
        {
            context.Add(new Note { Text = "Sauna" });
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(["Sauna"], await ReadNotesAsync());
    }

    [Fact]
    public async Task The_operation_runs_in_a_transaction()
    {
        var transactionCount = await ExecuteAsync((context, cancellationToken) =>
            context.Database.SqlQuery<int>($"SELECT @@TRANCOUNT AS Value").SingleAsync(cancellationToken));

        Assert.Equal(1, transactionCount);
    }

    [Fact]
    public async Task Nothing_is_saved_when_the_operation_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync<int>(async (context, cancellationToken) =>
        {
            context.Add(new Note { Text = "tracked" });
            await InsertNoteWithSqlAsync(context, "sql", cancellationToken);
            throw new InvalidOperationException("Boom.");
        }));

        Assert.Empty(await ReadNotesAsync());
    }

    [Fact]
    public async Task A_transient_failure_reruns_the_operation_without_the_failed_attempts_changes()
    {
        var attempts = 0;

        var result = await ExecuteAsync(async (context, cancellationToken) =>
        {
            attempts++;
            context.Add(new Note { Text = $"tracked {attempts}" });
            await InsertNoteWithSqlAsync(context, $"sql {attempts}", cancellationToken);

            if (attempts == 1)
            {
                await ThrowAsync(context, SimulatedTransientError, cancellationToken);
            }

            return attempts;
        });

        // The first attempt's SQL write was rolled back with its transaction, and its tracked
        // note was cleared from the change tracker rather than saved by the second attempt.
        Assert.Equal(2, result);
        Assert.Equal(["sql 2", "tracked 2"], await ReadNotesAsync());
    }

    [Fact]
    public async Task A_permanent_failure_is_not_retried()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(async (context, cancellationToken) =>
        {
            attempts++;
            context.Add(new Note { Text = "tracked" });
            await ThrowAsync(context, SimulatedPermanentError, cancellationToken);
            return attempts;
        }));

        Assert.Equal(SimulatedPermanentError, exception.Number);
        Assert.Equal(1, attempts);
        Assert.Empty(await ReadNotesAsync());
    }

    /// <summary>
    /// Runs <paramref name="operation"/> through the scope's unit of work, giving it the
    /// scope's <see cref="AppDbContext"/> as a command handler's repositories would use it.
    /// </summary>
    private async Task<TResult> ExecuteAsync<TResult>(Func<AppDbContext, CancellationToken, Task<TResult>> operation)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.ExecuteAsync(
            cancellationToken => operation(context, cancellationToken),
            TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> ReadNotesAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Set<Note>()
            .Select(note => note.Text)
            .OrderBy(text => text)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static Task InsertNoteWithSqlAsync(AppDbContext context, string text, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"INSERT INTO Notes (Id, Text) VALUES ({Guid.NewGuid()}, {text})",
            cancellationToken);

    private static Task ThrowAsync(AppDbContext context, int errorNumber, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync($"THROW {errorNumber}, 'Simulated failure.', 1;", cancellationToken);

    /// <summary>
    /// Registers Infrastructure as the API does, from a <c>Database</c> section built from the
    /// container's connection string. Adds <see cref="Note"/> to the model and treats
    /// <see cref="SimulatedTransientError"/> as transient, without delays between retries.
    /// </summary>
    private static ServiceProvider AddInfrastructure(string connectionString)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        var user = connection.UserID;
        var password = connection.Password;
        connection.Remove("User ID");
        connection.Remove("Password");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connection.ConnectionString,
                ["Database:Authentication"] = "Password",
                ["Database:User"] = user,
                ["Database:Password"] = password,
            })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure()
            .ConfigureDbContext<AppDbContext>(options => options
                .ReplaceService<IModelCustomizer, NoteModelCustomizer>()
                .UseSqlServer(sqlServer => sqlServer.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.Zero,
                    errorNumbersToAdd: [SimulatedTransientError])))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}

internal sealed class Note
{
    public Guid Id { get; init; }

    public required string Text { get; init; }
}

/// <summary>Maps <see cref="Note"/> in addition to the application's model.</summary>
internal sealed class NoteModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        modelBuilder.Entity<Note>(note =>
        {
            note.ToTable("Notes");
            note.Property(entity => entity.Text).HasMaxLength(100);
        });
    }
}
