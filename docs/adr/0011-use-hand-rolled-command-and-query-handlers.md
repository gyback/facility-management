# ADR-0011: Use hand-rolled command and query handlers

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0003 places use cases in the Application layer as commands and queries,
each with its own handler, organised by feature. It left open how an API
endpoint reaches the right handler: through a mediator library or through
handlers we write and wire up ourselves.

Forces:

- **Licensing.** MediatR, the de facto standard mediator for .NET, moved to a
  commercial licence from version 13 (2025), with a free community tier
  subject to eligibility terms. Other popular libraries in the same space
  have made similar moves. A dependency at the heart of every use case should
  not be able to change its licence terms under us, and the project should
  stay free to build, fork and show publicly without licence keys.
- **Size of the problem.** The core of a mediator is a handler interface and
  a lookup in the dependency injection container. ASP.NET Core's built-in
  container (ADR-0002) already resolves generic types, so the remaining work
  is small and well understood.
- **Cross-cutting concerns.** Validation, logging, and a transaction around
  each command are needed for every use case and should not be repeated in
  every handler.
- **Explicitness.** The project is a portfolio piece (ADR-0003). A reader
  should be able to go from an endpoint to its handler with "go to
  definition" rather than through a runtime lookup.
- **CQS, not CQRS.** Commands and queries are separated at the use-case
  level only. Both sides use the same EF Core model and database
  (ADR-0009); separate read stores or data access styles are out of scope.

## Decision

We will implement command/query separation ourselves in the Application
project, with no mediator library.

- **Contracts.** The Application project defines four small interfaces:
  `ICommand<TResult>`, `IQuery<TResult>`,
  `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`.
  A command without a meaningful return value returns a unit type.
- **Semantics.** Commands change state and return at most an identifier or
  an outcome, never a read model. Queries have no side effects and do not
  call `SaveChanges`.
- **Dispatch.** Endpoints depend directly on the handler interface they need,
  for example `ICommandHandler<CreateStay, StayId>`, injected by the
  container. There is no `ISender`/`IMediator` dispatcher type.
- **Cross-cutting concerns.** Validation, logging and the unit-of-work
  transaction are implemented as decorators around the handler interfaces,
  applied to all handlers in a fixed, explicit order.
- **Registration.** A single extension method in the Application project
  (for example `AddApplication()`) scans the assembly for handler
  implementations, registers them, and wraps them in the decorators. It is
  written by hand with reflection and covered by tests that assert every
  command and query has exactly one handler.

## Options considered

### Option 1: Hand-rolled handlers injected directly (chosen)

- Pros: No third-party dependency or licence to track; the full mechanism is
  a few small files we understand and can change; endpoints show their
  dependencies in their signatures; "go to definition" leads straight to the
  handler; decorators give the same cross-cutting behaviour a pipeline
  would; no runtime dictionary lookup per request.
- Cons: We own the registration and decorator code, including its tests;
  decorator wiring with the built-in container needs some care; no
  ecosystem of ready-made behaviours or documentation to lean on.

### Option 2: MediatR

- Pros: Widely known; familiar to reviewers; pipeline behaviours,
  notifications and stream requests out of the box; extensive community
  material.
- Cons: Commercial licence from version 13, with eligibility terms that can
  change; staying on the last open-source version means running an
  unmaintained dependency at the core of the application; indirection via
  `ISender.Send` hides which handler runs; brings features (notifications,
  streaming) we do not need.

### Option 3: An MIT-licensed mediator (e.g. source-generated `Mediator` or Wolverine)

- Pros: Open-source licence today; source-generated variants avoid reflection
  and catch missing handlers at compile time; Wolverine adds messaging if it
  is ever needed.
- Cons: Still a third-party dependency at the core whose licence and
  maintenance we do not control; smaller communities; Wolverine in
  particular is a much larger framework than this problem needs; keeps the
  indirection of a dispatcher.

### Option 4: Application services without a command/query split

- Pros: Least ceremony; one service class per feature with a method per use
  case.
- Cons: Services grow into large classes with many dependencies; the
  read/write separation that ADR-0003 calls for is lost; cross-cutting
  concerns must be applied per method.

## Consequences

### Positive

- No licence keys, eligibility checks or forced upgrades for the core of the
  application.
- Each endpoint declares the exact handler it uses, so the call chain is
  visible and navigable in the IDE.
- Handlers are plain classes, unit-testable without any framework.
- Transactions are applied consistently to commands by one decorator rather
  than by convention in each handler.

### Negative

- The registration and decorator code is ours to maintain and test.
- Reviewers familiar with MediatR need a short orientation; the Application
  project should document the convention.
- Features such as in-process notifications for domain events are not
  provided and must be built if and when they are needed.
- Decorating open generics with the built-in container takes more code than
  a library would; if it becomes a burden, a small MIT-licensed helper for
  registration only (for example Scrutor) may be adopted without changing
  the handler contracts.

### Follow-up

- Add the command/query contracts, the unit type, and `AddApplication()`
  registration to the Application project
  ([#25](https://github.com/gyback/facility-management/issues/25)).
- Implement the validation, logging and transaction decorators, and the
  registration tests
  ([#26](https://github.com/gyback/facility-management/issues/26)).
- Decide how domain events are dispatched once the first feature needs them
  ([#27](https://github.com/gyback/facility-management/issues/27)).

## References

- Issue [#2: Choose the command/query dispatch mechanism](https://github.com/gyback/facility-management/issues/2)
- [ADR-0002: Use C# and .NET for the backend](0002-use-csharp-and-dotnet-for-the-backend.md)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- Bertrand Meyer, Command–Query Separation, *Object-Oriented Software Construction*
- Jimmy Bogard, [AutoMapper and MediatR Going Commercial](https://www.jimmybogard.com/automapper-and-mediatr-going-commercial/)
