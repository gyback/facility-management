# FacilityManagementService.Application

Use cases, organised by feature (`Bookings/`, `Facilities/`, ...) per
[ADR-0003](../docs/adr/0003-use-clean-architecture.md). Each use case is a
command or a query with exactly one handler. There is no mediator library;
see [ADR-0011](../docs/adr/0011-use-hand-rolled-command-and-query-handlers.md).

## Convention

| | Command | Query |
|---|---|---|
| Message | `record CreateStay(...) : ICommand<StayId>` | `record GetStay(StayId Id) : IQuery<StayDetails>` |
| Handler | `ICommandHandler<CreateStay, StayId>` | `IQueryHandler<GetStay, StayDetails>` |
| Purpose | Changes state | Reads state |
| Returns | At most an identifier or an outcome; `Unit` if nothing | A read model |
| Side effects | Yes; the only place `SaveChanges` happens | None, never calls `SaveChanges` |

A use case lives in its feature folder as the message and its handler, e.g.
`Bookings/CreateStay.cs` and `Bookings/CreateStayHandler.cs`.

```csharp
public sealed record CreateStay(FacilityId FacilityId, DateOnly From, DateOnly To) : ICommand<StayId>;

internal sealed class CreateStayHandler(/* dependencies */) : ICommandHandler<CreateStay, StayId>
{
    public async Task<StayId> HandleAsync(CreateStay command, CancellationToken cancellationToken = default)
    {
        // ...
    }
}
```

## Dispatch and registration

- Endpoints inject the handler interface they need directly, e.g.
  `ICommandHandler<CreateStay, StayId>`. There is no `ISender`/`IMediator`;
  "go to definition" on the message type leads to its handler.
- `services.AddApplication()` (called from the API composition root) scans
  this assembly with reflection and registers every non-abstract handler as a
  scoped service. Handlers can be `internal`; nothing else needs registering.
- Registration fails at startup if two handlers handle the same command or
  query.
- Cross-cutting concerns (validation, logging, the unit-of-work transaction)
  will be applied as decorators around the handler interfaces
  ([#26](https://github.com/gyback/facility-management/issues/26)); handlers
  should not implement them themselves.
