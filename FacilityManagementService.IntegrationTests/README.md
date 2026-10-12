# FacilityManagementService.IntegrationTests

Tests that need a database: query handlers projecting from the EF Core model
([ADR-0014](../docs/adr/0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md)),
the exclusive-nights unique index
([ADR-0005](../docs/adr/0005-model-bookings-as-non-blocking-stays.md)) and
comment-thread integrity
([ADR-0013](../docs/adr/0013-share-comment-threads-across-features.md)).

They run against a real SQL Server in a container (Testcontainers), not the
in-memory provider or SQLite, so unique indexes and transactions behave as in
production. **A running Docker daemon is required**; `dotnet test` starts and
removes the container itself.

## Writing a test

`SqlServerFixture` is an assembly fixture: one container for the whole test
run. Each test class creates its own database from it, with every migration
applied by `Database.MigrateAsync()`, the same call the migration entry point
(#11) makes:

```csharp
public sealed class GetStayTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TestDatabase<AppDbContext> _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await sqlServer.CreateDatabaseAsync<AppDbContext>(
            options => new(options), TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
```

Arrange, act and assert through separate contexts from
`_database.CreateContext()`, so nothing is served from a change tracker.

`Probe/` holds a minimal model with its own migration that checks the harness
itself; it is not part of the application's schema.
