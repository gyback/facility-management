# ADR-0014: Use aggregate repositories for commands and projections for queries

- **Status:** Proposed
- **Date:** 2026-10-12
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0003 places "interfaces for infrastructure concerns (e.g. repositories)"
in the Application project, keeps that project dependent only on Domain, and
warns against speculative abstractions such as generic repositories.
ADR-0009 chooses EF Core with fluent configuration in Infrastructure, but
does not say what a handler depends on to read or write data. ADR-0011
splits use cases into commands and queries over the same EF Core model, and
wraps every command in a unit-of-work transaction decorator. That decorator
(#26) runs the handler through `IUnitOfWork.ExecuteAsync`, which saves and
commits the handler's changes, so command handlers do not call
`SaveChanges` themselves.

This has to be settled before the first feature writes data access code.

Forces:

- **Aggregate consistency.** The upkeep domain (ADR-0003) and the shared
  comment threads (ADR-0013) rely on aggregates that are always loaded and
  saved whole, so their invariants hold. A handler that can reach any table
  can load a `Comment` without its `CommentThread`, or a stay without the
  nights its exclusivity rule (ADR-0005) depends on.
- **Reads are a different shape.** Queries return read models: a calendar
  of overlapping stays, a thread rendered as a list, a balance summary.
  Loading full tracked aggregates and mapping them in memory over-fetches,
  and ADR-0009 already names projections and `AsNoTracking` as the way to
  keep EF Core queries efficient.
- **One place saves.** The unit of work is the transaction boundary. If
  handlers or repositories can also call `SaveChanges`, a command can commit
  half its work before the decorator's transaction ends, and a query can
  write.
- **Retries.** `IUnitOfWork.ExecuteAsync` takes a delegate so the
  implementation can re-run the whole operation under an EF Core execution
  strategy. Anything the handler touched must be safe to re-run.
- **Testability.** Command handlers hold most of the use-case logic and
  should be unit-testable without a database. Query handlers are mostly
  LINQ; their correctness depends on how the provider translates it.
- **Solo developer.** Every interface must pay for itself (ADR-0003).

## Decision

We will give command and query handlers different data access, both behind
the Application/Infrastructure boundary, with the unit of work as the only
caller of `SaveChanges`.

- **Commands use one repository per aggregate root.** For example
  `IStayRepository` and `ICommentThreadRepository`. The interface lives in
  the Application feature folder that owns the aggregate (`Bookings/`,
  `Comments/`) and declares only the operations its use cases need, such as
  `GetAsync(StayId)`, `Add(Stay)` and `Remove(Stay)`, plus the occasional
  aggregate-specific check such as "does an exclusive stay overlap these
  nights". There is no generic `IRepository<T>` and no base class. A
  repository always returns whole, tracked aggregates; which navigations to
  include is decided once, in its Infrastructure implementation.
- **Repositories never save.** No repository exposes `SaveChanges`. A
  repository only stages changes in the `DbContext` it shares with the
  `IUnitOfWork` implementation (both scoped), so changes a handler makes to
  the aggregates it loaded are picked up when the unit of work saves and
  commits. This keeps every write a command makes inside the `DbContext`,
  which is what `IUnitOfWork` requires to re-run a handler safely on a
  transient failure.
- **Queries project directly from the EF Core model.** Query handlers stay in
  the Application project and depend on a read-only `IReadDbContext`
  interface that exposes an `IQueryable<T>` per aggregate root. Its
  implementation returns queries with `AsNoTracking` applied, and has no
  `Add`, `Remove` or `SaveChanges`. Query handlers `Select` straight into
  their read model; they do not use the repositories.
- **The Application project references `Microsoft.EntityFrameworkCore`**
  (the core package, not a provider), so query handlers can use its async
  LINQ operators (`ToListAsync`, `FirstOrDefaultAsync`). Command handlers
  and repository interfaces do not use EF Core types. Entity configuration,
  the `DbContext` and all provider-specific code stay in Infrastructure.
- **Who calls `SaveChanges`.** Only the EF Core implementation of
  `IUnitOfWork`, once per command, inside the transaction.

## Options considered

### Option 1: Repositories for commands, projections for queries (chosen)

- Pros: Aggregate boundaries are enforced by what the interfaces return;
  include rules for each aggregate live in one place; command handlers are
  unit-testable with hand-written fakes; queries fetch only the columns the
  read model needs and never track; `SaveChanges` has exactly one caller;
  matches ADR-0003's "repositories in Application" without being generic.
- Cons: One interface and implementation per aggregate root; two styles of
  data access to learn; the Application project takes an EF Core package
  reference for the query side; query handlers need a real provider to
  test.

### Option 2: `IApplicationDbContext` exposing `DbSet`s for everything

The pattern popularised by the Clean Architecture solution templates:
Application defines an interface with a `DbSet<T>` per entity, implemented
by the `DbContext`.

- Pros: Least code; one data access style for commands and queries; full
  LINQ everywhere; no repository to update when a use case needs a new
  query shape.
- Cons: Any handler can load any entity, so aggregate boundaries are kept by
  convention only; include rules are repeated in each handler; `DbSet<T>`
  exposes `Add`/`Remove` to query handlers; `SaveChangesAsync` is usually on
  the interface and competes with the unit of work; command handlers can
  only be tested against a provider; Application depends on EF Core for
  writes as well as reads.

### Option 3: Repositories for both commands and queries

- Pros: No EF Core reference in Application at all; every handler is
  unit-testable with fakes; one abstraction.
- Cons: Repositories grow a read method per screen, which is the
  speculative, ever-widening repository ADR-0003 warns about; either queries
  load tracked aggregates and map in memory (over-fetching), or repositories
  return read models and stop being about aggregates.

### Option 4: Repositories for commands, query handlers implemented in Infrastructure

The query interface stays in Application; the handler implementation lives
in Infrastructure next to the `DbContext`.

- Pros: Application has no EF Core reference; queries still project
  directly with `AsNoTracking`.
- Cons: Splits each query use case across two projects, against ADR-0003's
  "commands, queries and their handlers live together"; handler
  registration (ADR-0011) has to scan a second assembly; decorators and the
  one-handler-per-query test must cover both.

## Consequences

### Positive

- An aggregate cannot be half-loaded by a command handler, so domain
  invariants (exclusive nights, comment rules, the expense ledger) are
  checked against complete state.
- Read models are shaped in SQL, and no query tracks entities or writes.
- The transaction boundary is unambiguous: one `SaveChanges`, in one class,
  per command.
- Command handlers, where most use-case logic sits, are tested with plain
  fakes.

### Negative

- The Application project depends on `Microsoft.EntityFrameworkCore`, a
  deliberate, read-side-only exception to ADR-0003's "depends only on
  Domain". It must not spread to command handlers or repository interfaces.
- Query handlers are tested as integration tests against a real provider,
  not with in-memory fakes.
- Each new aggregate root needs a repository interface and implementation.
- Read models project from the persistence model, so a schema change can
  break queries far from the aggregate that changed; tests over the query
  handlers are the safety net.

### Follow-up

- Add `IReadDbContext` to Application and the `Microsoft.EntityFrameworkCore`
  package reference.
- Implement `IUnitOfWork` over the `DbContext` in Infrastructure as its
  contract specifies, and register it alongside `IReadDbContext` as scoped.
- Add `IStayRepository` and its implementation with the first bookings
  slice.
- Set up integration tests for query handlers against SQL Server (for
  example with Testcontainers).

## References

- Issue [#38: Decide how handlers access data](https://github.com/gyback/facility-management/issues/38)
- Issue [#26: Add validation, logging and transaction decorators for handlers](https://github.com/gyback/facility-management/issues/26)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [ADR-0011: Use hand-rolled command and query handlers](0011-use-hand-rolled-command-and-query-handlers.md)
- [ADR-0013: Share comment threads across features](0013-share-comment-threads-across-features.md)
- Martin Fowler, [Repository](https://martinfowler.com/eaaCatalog/repository.html)
- Microsoft, [Connection resiliency and execution strategies](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency)
