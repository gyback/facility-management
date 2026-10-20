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
| Side effects | Yes; changes are saved by the transaction decorator | None, never calls `SaveChanges` |

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
  this assembly with reflection and registers every non-abstract handler and
  every `IValidator<T>` as a scoped service. Handlers and validators can be
  `internal`; nothing else needs registering.
- Registration fails at startup if two handlers handle the same command or
  query. A test asserts that every command and query has exactly one handler.
- Only the handler interface can be injected; the handler class itself is not
  resolvable, so nothing can bypass the decorators.
- Handlers and validators cannot be open generic classes, and a validator must
  validate the concrete command or query (not an interface it implements):
  validators run only for the exact message type. Registration fails otherwise.

## Decorators

Every handler is wrapped in decorators (`Messaging/Decorators/`), so handlers
contain only the use case itself. The order is fixed in
`DependencyInjection.CommandDecorators` / `QueryDecorators`, outermost first:

| | Command | Query |
|---|---|---|
| 1. Logging | Yes | Yes |
| 2. Validation | Yes | Yes |
| 3. Transaction | Yes | No |
| 4. Handler | | |

- **Logging** records the message type, duration and outcome (handled,
  rejected by validation, or failed). It never logs the message contents,
  which may contain personal data.
- **Validation** runs every `IValidator<TMessage>` and throws a
  `ValidationException` with all errors before anything else happens. Add a
  validator next to the message, e.g. `Bookings/CreateStayValidator.cs`.
- **Transaction** runs the command handler through `IUnitOfWork`, which saves
  and commits the handler's changes, or rolls back if it throws. Command
  handlers therefore do not call `SaveChanges` themselves. `IUnitOfWork` is
  implemented in Infrastructure, and the container fails to build until one is
  registered. `IUnitOfWork` may re-run a handler to retry a transient database
  failure, so a command handler must keep all its writes in the `DbContext`
  (no e-mails, HTTP calls or other side effects that cannot be repeated).

```csharp
internal sealed class CreateStayValidator : IValidator<CreateStay>
{
    public IEnumerable<ValidationError> Validate(CreateStay command)
    {
        if (command.To <= command.From)
        {
            yield return ValidationError.MustBeAfter(nameof(CreateStay.To), nameof(CreateStay.From));
        }
    }
}
```

### Validation codes

Every `ValidationError` has a `Code` from the shared set in `ValidationCodes`
(`required`, `too-long`, `out-of-range`, `must-be-after`), English `Message`
for developers and the typed `Parameters` its text needs, e.g. `maxLength`
([ADR-0017](../docs/adr/0017-use-minimal-apis-with-problem-details-warnings-and-idempotency-keys.md)).
The frontend translates the code; the message is never shown to users and
must never quote user input.

- Create errors with the factory for their code, e.g.
  `ValidationError.TooLong(nameof(AddComment.Body), 4000)`, rather than the
  constructor.
- Add a code to `ValidationCodes`, with a factory, only when no existing one
  fits. Each code needs an entry in the catalogue in the
  [API README](../FacilityManagementService.API/README.md#validation-codes).

## Data access

Per [ADR-0014](../docs/adr/0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md),
commands and queries reach the database differently, and neither calls
`SaveChanges`:

- **Command handlers** use the repository of the aggregate they change, e.g.
  `IStayRepository`, declared in the feature folder and implemented in
  Infrastructure. Repositories return whole, tracked aggregates and stage
  changes; the transaction decorator's `IUnitOfWork` saves them.
- **Query handlers** use `IReadDbContext` (`Persistence/`), which exposes an
  untracked `IQueryable<T>` per aggregate root, and `Select` straight into
  their read model with EF Core's async operators (`ToListAsync`, ...).
  `Microsoft.EntityFrameworkCore` is referenced for this alone; command
  handlers and repository interfaces must not use EF Core types.
- Add an aggregate root to `IReadDbContext` and its implementation when the
  feature that introduces it needs to query it.
