# ADR-0003: Use Clean Architecture

- **Status:** Proposed
- **Date:** 2026-09-21
- **Deciders:** Gustav Gybäck (sole developer)

## Context

The backend is a single deployable monolith that will grow to cover three
feature areas of very different complexity:

1. **Booking** — calendar-style reservations of cabins with comments and
   threads. Mostly CRUD plus an overlap constraint.
2. **Instructions** — documentation and how-to content per facility. Almost
   pure CRUD.
3. **Upkeep** — work logs, recurring maintenance schedules, planned
   renovations, and incurred costs split across multiple owners. This
   involves genuine domain rules: recurrence generation, an append-only
   expense ledger, split rules, and balances that must be auditable when
   relatives disagree about money.

The third area is expected to be heavy enough to warrant a clear separation
between domain logic, use cases and infrastructure. We prefer to use one
structural style consistently across the whole codebase rather than mixing
styles per feature. The project is also a public portfolio piece, so the
structure should be recognisable and defensible to reviewers.

A single developer is building this in spare time, so every additional layer
must pay for itself; ceremony that adds no testability or clarity is a cost.

## Decision

We will structure the backend as a Clean Architecture monolith with four
projects in the solution:

- `FacilityManagementService.Domain` — entities, value objects, domain
  events, domain services and invariants. No dependencies on other projects.
- `FacilityManagementService.Application` — use cases (commands and queries),
  their handlers, validation, and interfaces for infrastructure concerns
  (e.g. repositories, clock, current user). Depends only on Domain.
- `FacilityManagementService.Infrastructure` — EF Core persistence,
  external services, identity integration. Implements Application interfaces.
- `FacilityManagementService.API` — ASP.NET Core host, endpoints, request and
  response contracts, composition root.

Dependencies point inward only. The Application layer is organised by
feature (e.g. `Bookings/`, `Facilities/`, `Maintenance/`, `Expenses/`)
rather than by technical type, so that each feature's commands, queries and
handlers live together while still respecting the layer boundaries.

## Options considered

### Option 1: Clean Architecture (layered projects, dependency rule)

- Pros: Domain logic for cost splitting and recurrence is isolated and unit
  testable without a database; infrastructure (EF Core, identity) can be
  swapped or mocked; widely recognised structure that reads well on a
  portfolio; one consistent style for all features.
- Cons: More projects and more interfaces than the simpler features need;
  risk of over-abstraction (repository-per-entity, mapping layers) if applied
  dogmatically; slower to stand up the first vertical slice.

### Option 2: Modular monolith with vertical slices

- Pros: One folder per feature owning endpoints, handlers and EF entities;
  fewer interfaces and mapping layers; each slice can be as simple or as
  rich as it needs; often faster for a solo developer.
- Cons: Domain logic tends to leak into handlers unless discipline is high;
  the upkeep feature would likely need its own internal layering anyway,
  producing two styles in one codebase; less familiar to reviewers.

### Option 3: Simple layered (Controllers → Services → DbContext)

- Pros: Minimal ceremony; fastest to start; well understood.
- Cons: Business rules end up in services tied to EF Core, making the
  cost-splitting logic hard to test in isolation; tends to degrade into
  anaemic models and fat services as the upkeep feature grows.

## Consequences

### Positive

- The upkeep domain (ledger, split rules, recurrence) lives in a
  dependency-free Domain project and can be tested with plain unit tests.
- Persistence, identity and external integrations are replaceable without
  touching use cases.
- Feature-folder organisation inside Application keeps related code together
  and limits the "one file per layer per change" cost.

### Negative

- Simple CRUD features (instructions, most of booking) carry more structure
  than they strictly need.
- Initial setup is slower; the first end-to-end slice (booking) will take
  longer to ship than in a flat structure.
- Requires ongoing discipline to avoid speculative abstractions such as
  generic repositories or mapping every entity to a separate read model.

### Follow-up

- Create the Domain, Application and Infrastructure projects and reference
  them from the API project.
- Decide on the in-process dispatch mechanism for commands and queries
  (hand-rolled handlers vs. a mediator library) in a short follow-up ADR or
  README note.
- Build the booking feature end to end first to validate the structure
  before adding upkeep.

## References

- [ADR-0002: Use C# and .NET for the backend](0002-use-csharp-and-dotnet-for-the-backend.md)
- [ADR-0006: Use Entity Framework Core for data access](0006-use-entity-framework-core-for-data-access.md)
- Robert C. Martin, [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- Jimmy Bogard, [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
