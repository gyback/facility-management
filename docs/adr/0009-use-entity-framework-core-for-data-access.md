# ADR-0009: Use Entity Framework Core for data access

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

With Azure SQL Database chosen for production and SQL Server Express for
staging (ADR-0008) and a Clean Architecture layout (ADR-0003), the
Infrastructure project needs a data access approach. The domain model has a
moderate number of entities with rich relationships (facilities, members,
stays, schedules, occurrences, projects, expenses, shares). Most operations
are ordinary reads and writes of aggregates; a few (balance summaries,
overdue task lists, nightly occupancy) are reporting-style queries.

Forces:

- A single developer wants to spend time on domain logic, not on hand-written
  SQL and mapping code for every entity.
- Schema evolution must be repeatable and runnable as an explicit step in
  both environments.
- Domain entities in the Domain project should stay free of persistence
  attributes.
- The schema must stay engine-neutral (ADR-0007).

## Decision

We will use Entity Framework Core with the SQL Server provider for all data
access, with EF Core migrations as the single source of truth for the schema.

Entity configuration lives in the Infrastructure project using the fluent
API (`IEntityTypeConfiguration<T>`) so the Domain project stays persistence
agnostic. Migrations contain only what EF Core generates from the model, no
hand-written provider-specific SQL. Semi-structured content uses EF Core's
JSON column mapping rather than an engine-specific type. Dapper or raw SQL
may be introduced later for specific read-heavy reporting queries if
profiling shows EF Core is a bottleneck, but that is not expected at this
data volume.

Migrations are applied by a dedicated entry point in the API image, invoked
as a pre-deploy job on Azure and as a one-shot Compose service on the
homelab. The API itself does not apply migrations on startup.

## Options considered

### Option 1: Entity Framework Core

- Pros: Change tracking and unit-of-work semantics fit aggregate-style
  writes; migrations give versioned, automatable schema evolution; LINQ
  queries are type-checked; the SQL Server provider is first-party and the
  best supported; fluent configuration keeps the Domain project clean; the
  developer already knows it.
- Cons: Easy to generate inefficient queries without care (N+1,
  over-fetching); some learning curve around owned types, value objects and
  concurrency tokens.

### Option 2: Dapper (micro-ORM) with hand-written SQL

- Pros: Full control over every query; minimal abstraction; very fast.
- Cons: No change tracking, so every write is explicit SQL; no migrations,
  requiring a separate tool (DbUp, FluentMigrator, Flyway); mapping nested
  aggregates back from rows is manual and error-prone; hand-written SQL
  works against the engine-neutral rule.

### Option 3: EF Core for writes, Dapper for reads (CQRS-style split)

- Pros: Best of both for complex reporting.
- Cons: Two data access styles to maintain from day one for a workload that
  does not need it; premature for a few thousand rows. Kept as a future
  option rather than a starting point.

## Consequences

### Positive

- Schema changes are code-reviewed migrations, rehearsed on staging before
  production.
- Aggregates load and save with minimal boilerplate, leaving time for the
  domain logic.
- The Domain project has no reference to EF Core.
- Generated-only migrations are portable to another provider with a
  regeneration rather than a rewrite.

### Negative

- Query performance must be watched (projections, `AsNoTracking`, explicit
  includes) even though the data volume makes this unlikely to matter.
- Value objects (money, night ranges) need explicit EF configuration.
- Any invariant that EF Core cannot express as a unique index, foreign key
  or check constraint must live in application code inside a transaction.

### Follow-up

- Add `Microsoft.EntityFrameworkCore.SqlServer` and the design-time package
  to the Infrastructure project.
- Implement the migration entry point (for example `--migrate` on the API
  host) and wire it into the Azure pipeline and the Compose stack.
- Configure the exclusive-nights unique index (ADR-0005) in the initial
  bookings migration through the fluent API.

## References

- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [EF Core documentation](https://learn.microsoft.com/ef/core/)
