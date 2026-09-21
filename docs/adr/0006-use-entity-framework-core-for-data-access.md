# ADR-0006: Use Entity Framework Core for data access

- **Status:** Proposed
- **Date:** 2026-09-21
- **Deciders:** Gustav Gybäck (sole developer)

## Context

With PostgreSQL chosen (ADR-0005) and a Clean Architecture layout
(ADR-0003), the Infrastructure project needs a data access approach. The
domain model has a moderate number of entities with rich relationships
(facilities, members, bookings, schedules, occurrences, projects, expenses,
shares). Most operations are ordinary reads and writes of aggregates; a few
(balance summaries, overdue task lists) are reporting-style queries.

Forces:

- A single developer wants to spend time on domain logic, not on hand-written
  SQL and mapping code for every entity.
- Schema evolution must be repeatable and applied automatically in
  deployment.
- Domain entities in the Domain project should stay free of persistence
  attributes.
- A few PostgreSQL-specific features (exclusion constraints, `jsonb`) must
  be expressible.

## Decision

We will use Entity Framework Core with the Npgsql provider for all data
access, with EF Core migrations as the single source of truth for the schema.

Entity configuration lives in the Infrastructure project using the fluent
API (`IEntityTypeConfiguration<T>`) so the Domain project stays persistence
agnostic. PostgreSQL-specific constraints that EF Core cannot model are added
as raw SQL inside migrations. Dapper or raw SQL may be introduced later for
specific read-heavy reporting queries if profiling shows EF Core is a
bottleneck, but that is not expected at this data volume.

## Options considered

### Option 1: Entity Framework Core

- Pros: Change tracking and unit-of-work semantics fit aggregate-style
  writes; migrations give versioned, automatable schema evolution; LINQ
  queries are type-checked; Npgsql provider is first-class and maintained
  alongside EF Core; fluent configuration keeps the Domain project clean;
  the developer already knows it.
- Cons: Easy to generate inefficient queries without care (N+1, over-fetching);
  provider-specific features need raw SQL in migrations; some learning curve
  around owned types, value objects and concurrency tokens.

### Option 2: Dapper (micro-ORM) with hand-written SQL

- Pros: Full control over every query; minimal abstraction; very fast.
- Cons: No change tracking, so every write is explicit SQL; no migrations,
  requiring a separate tool (DbUp, FluentMigrator, Flyway); mapping nested
  aggregates back from rows is manual and error-prone; significantly more
  code for a solo developer.

### Option 3: EF Core for writes, Dapper for reads (CQRS-style split)

- Pros: Best of both for complex reporting.
- Cons: Two data access styles to maintain from day one for a workload that
  does not need it; premature for a few thousand rows. Kept as a future
  option rather than a starting point.

## Consequences

### Positive

- Schema changes are code-reviewed migrations, applied on startup or via a
  deploy step.
- Aggregates load and save with minimal boilerplate, leaving time for the
  domain logic.
- The Domain project has no reference to EF Core.

### Negative

- Query performance must be watched (projections, `AsNoTracking`, explicit
  includes) even though the data volume makes this unlikely to matter.
- PostgreSQL-specific SQL in migrations means migrations are not portable to
  other providers. Accepted per ADR-0005.
- Value objects (money, date ranges) need explicit EF configuration.

### Follow-up

- Add `Npgsql.EntityFrameworkCore.PostgreSQL` and
  `EFCore.NamingConventions` (snake_case) to the Infrastructure project.
- Decide how migrations are applied in deployment (on API startup vs. a
  one-shot migration container in Compose) as part of ADR-0007's follow-up.
- Configure the booking exclusion constraint via raw SQL in the initial
  bookings migration.

## References

- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0005: Use PostgreSQL](0005-use-postgresql.md)
- [EF Core documentation](https://learn.microsoft.com/ef/core/)
