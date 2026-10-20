# FacilityManagementService.API

The ASP.NET Core host: endpoints, request and response contracts, and the
HTTP conventions of
[ADR-0017](../docs/adr/0017-use-minimal-apis-with-problem-details-warnings-and-idempotency-keys.md).
`Program.cs` calls `AddApplication()`, `AddInfrastructure()` and `AddApi()`,
then `UseApi()`, and maps each feature's endpoints.

## Endpoints

Endpoints are minimal APIs, organised by feature like Application
(`Facilities/`, `Bookings/`, ...). Each feature has a static class that maps
its endpoints, called once from `Program.cs`:

```csharp
internal static class StayEndpoints
{
    public static IEndpointRouteBuilder MapStayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/facilities/{facilityId:guid}/stays", CreateStay)
            .WithName(nameof(Application.Bookings.CreateStay))
            .WithTags("Stays")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var stays = app.MapGroup("/stays/{stayId:guid}").WithTags("Stays");
        stays.MapGet("", GetStay).WithName(nameof(Application.Bookings.GetStay));
        // ...
        return app;
    }

    private static async Task<Created<CreatedResponse>> CreateStay(
        Guid facilityId,
        CreateStayRequest request,
        ICommandHandler<Application.Bookings.CreateStay, Outcome<StayId>> handler,
        CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(request.ToCommand(facilityId), cancellationToken);
        return TypedResults.Created($"/stays/{outcome.Value}", CreatedResponse.From(outcome));
    }
}
```

- Each endpoint is a **named static method**, not a lambda. Its parameters
  are the route values, the request contract, the handler interface it calls
  ([ADR-0011](../docs/adr/0011-use-hand-rolled-command-and-query-handlers.md))
  and the `CancellationToken`. It maps the request to the command or query,
  calls the handler and maps the result. It holds no business logic.
- It returns `TypedResults` with a `Results<...>` return type, so OpenAPI
  learns every success response from the signature.
- It declares the problems it can return with `.ProducesProblem(status)`.
  Every operation also gets a `500`, and every `POST` a `400`, without
  declaring them.
- Its operation id (`WithName`) is the name of the command or query it
  calls, so the generated client uses the Application's vocabulary. A
  feature's routes share a `MapGroup` and an OpenAPI tag.

## Routes and methods

- Paths are lower-case plural nouns in kebab-case, with no `/api` prefix
  and no version segment.
- A collection that belongs to a facility is reached through it
  (`GET /facilities/{facilityId}/stays`). A single resource is reached by its
  own id, without its parents (`GET /stays/{stayId}`). Things owned by a
  resource hang off it (`/stays/{stayId}/comments/{commentId}`).
- Ids in routes are constrained (`{stayId:guid}`), so an id of the wrong
  form does not match the route and gets `404 not-found`.
- Creating is `POST` on the collection, answered with `201 Created` and a
  `Location` header. Deleting is `DELETE` on the resource. A command that
  sets values given in full is `PUT`. Any other command is `POST` on a named
  action, e.g. `POST /stays/{stayId}/confirm`. There is no `PATCH`.
- Queries are `GET`, with filters in the query string.
- Secrets never go in a URL; an invitation token goes in the body of
  `POST /invitations/accept`.

## Contracts and wire format

- Request and response contracts are records next to their feature's
  endpoints. Commands, queries and read models never appear in an endpoint
  signature or the OpenAPI document.
- Mapping is written by hand: `ToCommand(...)` on the request, a static
  `From(...)` on the response. A request property has the same name as the
  command property it feeds, so validation errors point at the field the
  client sent.
- Property names are camelCase, and every declared property is always
  present, `null` when it has no value. Enums are camelCase strings. Dates
  without a time are `yyyy-MM-dd`. Points in time are ISO 8601 in UTC with a
  `Z`; convert in `From(...)`. Ids are strings.
- Validation stays in Application validators; there are no data
  annotations. The API only rejects a body it cannot parse, or a query
  string value of the wrong type, as `malformed-request`.

## Command responses and warnings

Every command response has a body with a `warnings` array, always present
and empty when there is nothing to warn about. Command endpoints never
answer `204`.

| Command | Status | Body |
|---|---|---|
| Creates something | `201 Created` with `Location` | `CreatedResponse`: `id` and `warnings` |
| Returns nothing else | `200 OK` | `CommandResponse`: `warnings` |
| Returns an outcome | `200 OK` | Its own response record, with a `Warnings` property |

```json
{
  "id": "6f1c9d0e-4b9a-4f57-9c1e-2a4b8f3d7e10",
  "warnings": [
    {
      "code": "capacity-exceeded",
      "message": "3 nights exceed the facility's capacity of 12.",
      "nights": ["2027-07-10", "2027-07-11", "2027-07-12"],
      "capacity": 12
    }
  ]
}
```

A warning means the command succeeded and was committed. A command that can
warn returns `Outcome<T>` (see the
[Application README](../FacilityManagementService.Application/README.md#results-warnings-and-refusals)),
and `WarningResponse.From` maps each warning to its contract. `WarningResponse`
is polymorphic on `code`, so OpenAPI shows a union of the known warnings.

## Errors

Every error is an RFC 9457 problem (`application/problem+json`), written by
ASP.NET Core's problem details service. No endpoint builds an error body.
Handlers and domain code throw; `ApiExceptionHandler` maps a closed list of
exceptions, and anything else is `500 internal-error`. A response with only
an error status, such as an unmatched route, becomes a problem too.

Every problem has `type` (a link to its entry below), `title` (fixed per
code), `status`, `detail`, `code` and `traceId`, plus any fields of its own:

```json
{
  "type": "https://github.com/gyback/facility-management/blob/master/FacilityManagementService.API/README.md#validation-failed",
  "title": "The request failed validation.",
  "status": 400,
  "detail": "The request failed validation.",
  "code": "validation-failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "body": [
      { "code": "too-long", "message": "Must be at most 4000 characters.", "parameters": { "maxLength": 4000 } }
    ]
  }
}
```

- The frontend switches on `code`, and uses it as its translation key.
  `title`, `detail` and `message` are English, for developers and logs. They
  are never shown to users, and `detail` never quotes user input. A code the
  frontend does not know falls back to a generic message for the status.
- Values a message needs are typed fields (dates as `yyyy-MM-dd`, counts as
  numbers), never formatted text.
- A request cancelled by the client gets no body.

Adding a problem code means a constant in `ProblemCodes` (the API's own) or
`RuleCodes` (Domain), and an entry in the catalogue below. A test fails until
both match.

## Idempotency keys

Every `POST` needs an `Idempotency-Key` header: 1 to 64 visible ASCII
characters. The SPA sends a new UUID per user action, and reuses it for
every retry of that action. Without one, or with a malformed one, the
request is rejected before the endpoint runs. `PUT`, `DELETE` and `GET`
ignore the header. Testing a `POST` by hand therefore needs the header, e.g.
`Idempotency-Key: {{$random.uuid}}` in the `.http` file.

Storing keys and replaying the original response to a retry is
[#50](https://github.com/gyback/facility-management/issues/50). Until then the
key is required but not yet honoured.

## CORS

The SPA is on another origin, so it can call the API only from the origins
in the `Cors` section, which is validated when the host starts:

| Key | Value |
|---|---|
| `Cors:AllowedOrigins` | Origins such as `https://app.example.com`, in lower case, without a path or trailing slash (environment variables `Cors__AllowedOrigins__0`, ...). Empty allows no cross-origin calls. |

`appsettings.Development.json` allows `http://localhost:5173`, the SPA's
development server. The policy allows `GET`, `POST`, `PUT` and `DELETE`, the
`Content-Type`, `Authorization` and `Idempotency-Key` request headers, and
exposes the `Location` and `Retry-After` response headers.

## OpenAPI

The document is served at `/openapi/v1.json` in every environment,
anonymously. Problem responses use one `Problem` schema whose `code` is the
`ProblemCode` enum; validation errors use the `ValidationErrorCode` enum.
Every `POST` declares the required `Idempotency-Key` header. Writing the
document at build time for the client generator is
[#17](https://github.com/gyback/facility-management/issues/17).

## Catalogue

Each problem's `type` links to its entry here.

### Problems

#### malformed-request

`400` (or `415` for an unsupported content type). The body could not be
parsed, or a query string value has the wrong type. Fix the request.

#### validation-failed

`400`. A validator rejected the command or query. `errors` lists each
property's errors by its camelCase name; see [Validation codes](#validation-codes).

#### idempotency-key-missing

`400`. A `POST` without an `Idempotency-Key` header.

#### idempotency-key-invalid

`400`. The `Idempotency-Key` header is not a single value of 1 to 64 visible
ASCII characters.

#### unauthenticated

`401`. The request has no valid token.

#### forbidden

`403`. The caller is not allowed to do this.

#### not-found

`404`. The target does not exist, or the route does not match, e.g. an id
that is not a GUID.

#### method-not-allowed

`405`. The route exists, but not for this method.

#### conflict

`409`. A unique index rejected the change, typically because a concurrent
request made a conflicting change first. Reload and try again.

#### internal-error

`500`. Anything else. Outside Development, `detail` reveals nothing about
the cause; use `traceId` to find it in the telemetry.

### Warnings

None yet. The capacity warning comes with the booking feature
([#8](https://github.com/gyback/facility-management/issues/8)).

### Validation codes

The codes of the errors in a `validation-failed` problem, with the
`parameters` each one carries.

#### required

The property has no value. No parameters.

#### too-long

The text is longer than allowed. `maxLength`: the most characters allowed.

#### out-of-range

The value is outside the allowed range. `minimum` and `maximum`: the bounds.

#### must-be-after

The value must be after another property's value, e.g. a stay's end after
its start. `otherProperty`: the camelCase name of the other property.
