# FacilityManagementService.Domain

Entities, value objects and domain rules, organised by feature (`Bookings/`,
`Facilities/`, ...) per [ADR-0003](../docs/adr/0003-use-clean-architecture.md).
This project references nothing: no other project, no EF Core, no packages
(`DomainDependencyTests` checks this).

## Entities and ids

Per [ADR-0018](../docs/adr/0018-use-domain-generated-version-7-guids-as-strongly-typed-ids.md),
every entity is identified by a version 7 `Guid` that the domain generates
when the entity is constructed, wrapped in a strongly typed id.

| Type (`Entities/`) | Role |
|---|---|
| `IEntityId<TSelf>` | Implemented by every id: `Guid Value` and a static `From(Guid)`. |
| `Entity<TId>` | Base class of everything with an id. Rejects an empty id. |
| `AggregateRoot<TId>` | Base class of aggregate roots, the only entities repositories load and `IReadDbContext` exposes (ADR-0014). |

### Adding an id

Write the id in the feature folder of the entity it identifies, e.g.
`Bookings/StayId.cs`, exactly like this:

```csharp
public readonly record struct StayId(Guid Value) : IEntityId<StayId>
{
    public static StayId New() => new(Guid.CreateVersion7());
    public static StayId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

- `New()` creates an id for a new entity; `From(guid)` only wraps an existing
  one (a route value, a test, the EF Core converter).
- `ToString()` returns the bare GUID, so ids read normally in logs and
  `Location` headers.
- `EntityIdConformanceTests` fails if an id is not a `readonly record struct`,
  if `New()` does not return a version 7 id, or if `ToString()` is not the
  bare GUID.
- Nothing else is needed: Infrastructure maps every id by convention.

### Using ids

- An aggregate's factory or constructor calls `New()` and passes the id to
  the base constructor; the id never changes afterwards. A handler that
  creates related aggregates creates them in order and passes ids along:
  `var thread = CommentThread.Start(); var stay = Stay.Book(..., thread.Id);`.
- `Entity<TId>` has no parameterless constructor, so no entity can exist with
  an empty id. Give each entity a private constructor that takes only its id
  and passes it to the base; EF Core binds it to the key when it loads the
  entity and sets the rest of the state afterwards.
- Commands, queries, repository methods, domain methods and read models take
  typed ids. In Domain and Application a raw `Guid` for an entity's identity
  appears only inside an id type.
- Entities do not override `Equals`; compare ids (`a.Id == b.Id`).
- Nothing relies on the order of ids. Anything that needs an order sorts by a
  timestamp and uses the id only as a tie-breaker.
- Something identified by its values rather than addressed on its own (e.g.
  the exclusive-nights rows of ADR-0005) has a composite key on those values
  and no id; it does not derive from `Entity<TId>`.
