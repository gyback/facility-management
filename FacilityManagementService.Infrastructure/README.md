# FacilityManagementService.Infrastructure

EF Core persistence and other infrastructure behind the Application
interfaces, per [ADR-0009](../docs/adr/0009-use-entity-framework-core-for-data-access.md)
and [ADR-0014](../docs/adr/0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md).
`services.AddInfrastructure()` (called from the API composition root)
registers everything below.

## Persistence

| Type | Role |
|---|---|
| `AppDbContext` | The EF Core model. Applies every `IEntityTypeConfiguration<T>` in this assembly. Internal, so nothing outside Infrastructure can reach it. |
| `UnitOfWork` : `IUnitOfWork` | Runs each command in one transaction under the SQL Server retrying execution strategy, and is the only caller of `SaveChanges`. On a transient failure it clears the change tracker and re-runs the whole command. |
| `ReadDbContext` : `IReadDbContext` | Untracked `IQueryable<T>` per aggregate root for query handlers. |
| Repositories | One per aggregate root, added with its feature. They stage changes in the scoped `AppDbContext` and never save. |

Map an entity by adding an `IEntityTypeConfiguration<T>` in this project;
keep it engine-neutral (ADR-0007, constraint 4).

## Configuration

The connection is configured from the `Database` section (environment
variables `Database__ConnectionString` etc.) and validated when the host
starts. The connection string names the server and database only; how to
sign in is chosen by `Authentication` (ADR-0007, constraint 5).

| Key | Value |
|---|---|
| `Database:ConnectionString` | Server, database and transport settings, e.g. `Server=localhost,1433;Database=FacilityManagement;Encrypt=True;TrustServerCertificate=True`. Must not contain credentials. |
| `Database:Authentication` | `Password` (homelab, local development). Managed identity for Azure is [#15](https://github.com/gyback/facility-management/issues/15). |
| `Database:User`, `Database:Password` | The SQL login, for `Password`. |

For local development, `appsettings.Development.json` points at SQL Server on
`localhost,1433` as `sa`; set the password with
`dotnet user-secrets set Database:Password <password> --project FacilityManagementService.API`.

`TimeProvider.System` is registered as the application clock; inject
`TimeProvider` rather than reading `DateTime.UtcNow`.

## Migrations

Migrations live in `Persistence/Migrations` and are generated, never
hand-written (ADR-0009). Restore the local `dotnet-ef` tool with
`dotnet tool restore`, then from the repository root:

```shell
dotnet ef migrations add <Name> --project FacilityManagementService.Infrastructure --startup-project FacilityManagementService.Infrastructure --output-dir Persistence/Migrations
```

Creating migrations needs no database. To apply them by hand, add
`database update --connection "<connection string with credentials>"`; in
deployments they are applied by the migration entry point
([#11](https://github.com/gyback/facility-management/issues/11)), never by
the API on startup.

## Tests

`FacilityManagementService.Infrastructure.Tests` covers what needs no
database: the `Database` settings validation, the `AddInfrastructure()`
registrations and a check that the model has no changes missing a migration.
`FacilityManagementService.IntegrationTests` runs `UnitOfWork` against SQL
Server: commit, rollback, and the retry after a transient failure.
