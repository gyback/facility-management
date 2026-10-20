# ADR-0018: Use domain-generated version 7 GUIDs as strongly typed IDs

- **Status:** Proposed
- **Date:** 2026-10-20
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0011 writes handlers as `ICommandHandler<CreateStay, StayId>`, and
ADR-0017 writes routes as `{stayId:guid}` and puts ids on the wire as
strings. No ADR says what an id is, who creates it, or how it is mapped.
#42 is about to add the entity base and the first ids. The choice sets every
table's primary and foreign keys, every aggregate's constructor and every
handler signature, and it is expensive to change once data exists.

Forces:

- **The handler returns the id before anything is saved.** `UnitOfWork` runs
  the handler, then calls `SaveChanges`, then commits (ADR-0014). A
  `CreateStay` handler that returns a `StayId` has to know the id while the
  stay is still only tracked.
- **Several aggregates are created together.** A stay and its comment thread
  are created in one unit of work, and the foreign key points from the stay
  to the thread (ADR-0013). The stay needs the thread's id when it is
  constructed, and the domain has no navigation property to the thread.
- **Handlers can run twice.** `UnitOfWork` re-runs the whole handler on a
  transient failure, after clearing the change tracker and rolling back
  (ADR-0014). Whatever produces an id must be safe to call again.
- **Engine-neutral schema.** Migrations contain only what EF Core generates,
  and the schema must move between SQL Server and another engine
  (ADR-0007, ADR-0009). Defaults such as `NEWSEQUENTIALID()` are
  engine-specific SQL.
- **SQL Server orders GUIDs oddly.** `uniqueidentifier` values are compared
  starting from the last six bytes, then the bytes before them, with the
  first four bytes last. A version 7 GUID keeps its timestamp in the first
  six bytes, so on SQL Server it sorts no better than a random one.
  PostgreSQL compares `uuid` byte by byte, so there the same values sort by
  creation time.
- **Small data.** ADR-0009 expects a few thousand rows. Index fragmentation
  from non-sequential keys will not be measurable.
- **Ids are visible.** They appear in URLs, responses and the frontend's
  state. ADR-0015 answers anything outside the caller's facilities with
  `404`, so an id is never the only protection, but sequential ids still
  reveal how many stays or users exist and invite guessing.
- **Ids of different entities get mixed up.** `Reschedule(Guid stayId, Guid
  facilityId)` compiles with its arguments swapped. Commands, repository
  methods and domain methods (ADR-0015's `by: userId`) take several ids at
  once.
- **Wire contracts are API-owned.** Application types never appear in an
  endpoint signature or in the OpenAPI document (ADR-0017).
- **Dependencies.** ADR-0011 avoided a mediator, and ADR-0017 a mapping
  library, partly to keep third-party code out of the core of the
  application.
- **Solo developer.** Every convention must be cheap to follow and easy to
  check in review.

## Decision

We will identify every entity by a **GUID that the domain generates when the
entity is constructed**, using **version 7** (`Guid.CreateVersion7()`). Each
entity type has its own **hand-written strongly typed id**, a `readonly
record struct` wrapping the `Guid`. One EF Core convention maps all of them.
They stay inside Domain and Application; the API binds and returns plain
`Guid`s.

### The key type

- Every entity id is a `Guid`, stored as `uniqueidentifier` on SQL Server
  (and `uuid` on PostgreSQL, if ADR-0007's fallback engine is ever needed).
  There are no `int` identity keys.
- New ids are version 7. **Nothing relies on their order.** On SQL Server
  they cluster no better than version 4, and that is accepted (see
  Consequences). Anything that needs an order sorts by a timestamp, as
  ADR-0016 already does for comments, and uses the id only as a
  tie-breaker.
- Rows that belong to an aggregate and are identified by their values, such
  as the exclusive-nights rows (ADR-0005), have a composite key on those
  values instead of an id of their own. An id is for something that is
  addressed on its own: an aggregate root, or an entity a route or another
  table refers to, such as a comment.

### Who generates the id

- **The entity, when it is constructed.** An aggregate's factory or
  constructor calls `StayId.New()` and the id is fixed from then on. EF Core
  and the database never generate ids.
- A handler that creates related aggregates creates them in order and passes
  ids along: `var thread = CommentThread.Start(); var stay = Stay.Book(...,
  thread.Id);`. Both are added through their repositories and saved in the
  same unit of work.
- A retried handler constructs new entities with new ids. The failed
  attempt was rolled back and its tracked entities discarded, so the old
  ids never existed. A retried commit that had in fact succeeded creates a
  second entity under a new id; preventing that is the idempotency work
  (#50, ADR-0017), not the id's job.
- Rehydrating an existing id (from a route value, in a test, in EF Core's
  converter) uses `StayId.From(guid)`. `From` does not create anything; it
  only wraps.

### Strongly typed ids

Each id is written by hand in the Domain feature folder of the entity it
identifies:

```csharp
public readonly record struct StayId(Guid Value) : IEntityId<StayId>
{
    public static StayId New() => new(Guid.CreateVersion7());
    public static StayId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

- `IEntityId<TSelf>` lives in Domain next to the entity base. It exposes
  `Guid Value` and a static abstract `From(Guid)`, which is all the EF Core
  convention needs to convert any id generically.
- `ToString()` returns the bare GUID, so ids read normally in logs and
  exception messages.
- `default(StayId)` wraps `Guid.Empty` and cannot be prevented in a struct.
  The entity base rejects an empty id in its constructor, so no entity can
  exist with one.
- Commands, queries, repository methods, domain methods and read models use
  the typed ids. A raw `Guid` for an entity's identity appears in Domain or
  Application only inside an id type.
- No source generator is used. An id is five lines, written once per entity
  type, and the converter is written once.

### Mapping

- **One convention in Infrastructure, no per-entity configuration.**
  `AppDbContext` finds every property whose type implements `IEntityId<>`,
  keys and foreign keys alike, and gives it one generic value converter to
  `Guid`. Every key of such a type is marked `ValueGeneratedNever()`.
  Entity configurations (ADR-0009) never mention an id's conversion.
- `ValueGeneratedNever()` is required, not a detail. If EF Core believes a
  key is generated, an entity with its key already set that it discovers
  through a navigation, such as a new `Comment` added to a tracked
  `CommentThread`, is treated as existing and updated instead of inserted.
- Queries compare whole ids (`s.FacilityId == facilityId`). EF Core cannot
  translate `.Value` on a converted property, so `.Value` never appears
  inside a LINQ query.
- A test over the model asserts that every `IEntityId<>` property has the
  converter and that no such key is generated by the store.

### On the wire

- ADR-0017 stands unchanged: ids are strings on the wire. Endpoints bind
  route values as `Guid` (`{stayId:guid}`), and request contracts declare
  `Guid` properties. The request's `ToCommand(...)` wraps them with
  `From(...)`, and the response's `From(...)` unwraps `.Value`.
- Typed ids therefore need no JSON converter, no `TryParse` for route
  binding and no OpenAPI schema transformer, and the generated client sees
  `string` with format `uuid`.

### Equality

- **Entities do not override `Equals` or `GetHashCode`.** Code that asks
  whether two entities are the same compares their ids (`a.Id == b.Id`),
  which the record struct makes a value comparison.
- Within a unit of work EF Core already returns one instance per id, so
  reference equality and id equality agree for everything a command
  handler loads. The read side works with read models, not entities
  (ADR-0014).

## Options considered

### Key type

#### Option 1: Version 7 `Guid` (chosen)

- Pros: Generated in process, so the domain knows the id before saving;
  unguessable and reveals no counts; `Guid` maps natively on SQL Server and
  PostgreSQL; built into .NET (ADR-0012), no package; sorts by creation on
  engines that compare bytes in order, which costs nothing to keep for
  ADR-0007's fallback; the embedded timestamp helps when reading logs.
- Cons: No index locality on SQL Server; 16 bytes per key and foreign key;
  reveals creation time to the millisecond to anyone who can see the id.
  The members who can see a stay or comment already see when it was made.

#### Option 2: Version 4 `Guid`

- Pros: Everything Option 1 has on SQL Server; no embedded timestamp.
- Cons: Random on every engine, so a move to PostgreSQL gives up the
  locality v7 would have had there for free.

#### Option 3: SQL Server-ordered sequential `Guid`

Generated in the order SQL Server sorts, as `NEWSEQUENTIALID()` or EF Core's
`SequentialGuidValueGenerator` do.

- Pros: Index locality on SQL Server.
- Cons: The database default is engine-specific SQL (ADR-0007); EF Core's
  generator runs at `Add` time in Infrastructure, not in the domain, so the
  id must either be generated by EF Core (see "Who generates the id") or
  the byte layout re-implemented in Domain; ordering tied to one engine is the opposite of
  portable; locality does not matter at this data volume.

#### Option 4: `int` identity

- Pros: Small keys; sequential inserts; readable in SQL.
- Cons: Known only after `SaveChanges`, which runs after the handler has
  returned (ADR-0014); a stay cannot be given its thread's id when
  constructed (ADR-0013); reveals counts and invites guessing; ADR-0017's
  routes and the OpenAPI document already assume GUIDs.

#### Option 5: ULID

- Pros: Time-ordered, shorter string form.
- Cons: Not a .NET type: a package or our own implementation, and a
  converter to `uniqueidentifier` or a `char(26)` column; its order is lost
  on SQL Server just as v7's is; offers nothing v7 does not.

### Who generates the id

#### Option 1: The domain, at construction (chosen)

- Pros: An entity is complete and valid from its constructor, including its
  id; handlers can return the id and link new aggregates before anything is
  saved; nothing depends on EF Core's value generation; domain tests see
  real ids.
- Cons: Tests that need a known id must construct entities with one
  (`From`) or read it back; ids are not deterministic across retries, which
  nothing needs.

#### Option 2: EF Core or the database, on save

- Pros: The usual EF Core default; no id code in the domain.
- Cons: With identity keys, the id does not exist until `SaveChanges`, after
  the handler returns; with client-side GUID generation, it exists only
  after `Add`, so a constructed entity has a placeholder id; links between
  new aggregates would need navigation properties the domain does not have
  (ADR-0013).

#### Option 3: The client sends the id

The SPA generates the id and creates with `PUT /stays/{stayId}`.

- Pros: Creates are naturally idempotent.
- Cons: ADR-0017 already chose `POST` with an `Idempotency-Key` for that;
  the server must check that a client-chosen id is not already taken by
  something else, possibly in another facility the caller cannot see.

### How strongly typed ids are written

#### Option 1: Hand-written `readonly record struct`s (chosen)

- Pros: No dependency; the code is short and visible, and "go to
  definition" shows all of it; value equality, `==` and `GetHashCode` come
  from the record struct; one generic converter covers every id.
- Cons: Five lines per id, copied by hand; nothing stops `default`, so the
  entity base must check.

#### Option 2: A source generator (StronglyTypedId, Vogen)

- Pros: Less code per id; can generate EF Core converters, JSON converters
  and parsing; Vogen can forbid `default` with analyzers.
- Cons: A third-party package in Domain, the one project ADR-0003 keeps
  free of dependencies; generated code to understand and debug; most of
  what they generate (JSON, parsing, OpenAPI) is not needed because ids stay
  off the wire; the code they save is a few lines per entity type.

#### Option 3: Plain `Guid`s

- Pros: No types and no converter.
- Cons: Ids of different entities are interchangeable, so swapped arguments
  compile; signatures such as `ICommandHandler<CreateStay, Guid>` say less.

### Equality

#### Option 1: Reference equality, compare ids explicitly (chosen)

- Pros: No base-class magic; what a comparison means is visible at the call
  site; EF Core's identity resolution already makes tracked instances
  unique per id.
- Cons: `entities.Contains(entity)` across two `DbContext`s compares
  references; such code must compare ids.

#### Option 2: Entity base overrides `Equals` by id

- Pros: Two instances of the same stay are equal everywhere.
- Cons: Hides a choice behind `==`; equality depending on type checks
  across EF Core proxies and inheritance is easy to get subtly wrong; no
  current use case needs it.

## Consequences

### Positive

- An entity has its final id from the moment it exists, so handlers return
  ids and link new aggregates without saving first.
- Swapping a `StayId` and a `FacilityId` is a compile error.
- The schema has no generated key columns or engine-specific defaults.
- Ids add nothing to the wire format, the OpenAPI document or the generated
  client.
- Ids reveal no counts and cannot be enumerated.

### Negative

- Every primary key on SQL Server is effectively random, so clustered
  indexes fragment and inserts split pages. At the expected volume this is
  not measurable. If it ever is, a new ADR can move the clustered index to
  another column without changing the ids.
- Keys are 16 bytes, in every foreign key and index.
- Each new entity type needs its id struct, and every id needs `New()`
  and `From()` written the same way. A test should check that every
  `IEntityId<>` is a `readonly record struct` and that `New()` returns a
  version 7 id.
- The `.Value`-in-LINQ rule and the explicit `ValueGeneratedNever()` are
  EF Core details a contributor has to know; the model test catches the
  second, not the first.
- Version 7 ids expose creation time to the millisecond.

### Follow-up

- Add `IEntityId<TSelf>`, the entity base with its empty-id check, the
  EF Core convention and the model test
  ([#42](https://github.com/gyback/facility-management/issues/42)).

## References

- Issue [#55: Decide the identifier type and strongly typed ID convention](https://github.com/gyback/facility-management/issues/55)
- Issue [#42: Add domain building blocks: entity base and strongly typed IDs](https://github.com/gyback/facility-management/issues/42)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [ADR-0011: Use hand-rolled command and query handlers](0011-use-hand-rolled-command-and-query-handlers.md)
- [ADR-0012: Target the latest LTS release of .NET](0012-target-the-latest-lts-release-of-dotnet.md)
- [ADR-0013: Share comment threads across features](0013-share-comment-threads-across-features.md)
- [ADR-0014: Use aggregate repositories for commands and projections for queries](0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md)
- [ADR-0015: Authenticate with Entra External ID and link identities to users by invitation](0015-authenticate-with-entra-external-id-and-invitation-linked-users.md)
- [ADR-0016: Keep the history of edited and deleted comments](0016-keep-the-history-of-edited-and-deleted-comments.md)
- [ADR-0017: Use minimal APIs with problem details, warnings and idempotency keys](0017-use-minimal-apis-with-problem-details-warnings-and-idempotency-keys.md)
- IETF, [RFC 9562: Universally Unique IDentifiers (UUIDs)](https://www.rfc-editor.org/rfc/rfc9562)
- Microsoft, [`Guid.CreateVersion7`](https://learn.microsoft.com/dotnet/api/system.guid.createversion7)
- Microsoft, [Value conversions](https://learn.microsoft.com/ef/core/modeling/value-conversions)
- Microsoft, [Comparing GUID and uniqueidentifier values](https://learn.microsoft.com/sql/connect/ado-net/sql/compare-guid-uniqueidentifier-values)
