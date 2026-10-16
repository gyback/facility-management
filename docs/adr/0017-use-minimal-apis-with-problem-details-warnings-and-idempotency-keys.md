# ADR-0017: Use minimal APIs with problem details, warnings and idempotency keys

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0003 puts the ASP.NET Core host, the endpoints and the request and
response contracts in the API project. ADR-0011 has each endpoint inject the
exact handler interface it calls, with no dispatcher. No ADR says what an
endpoint looks like, what goes over the wire, or how a failure reaches the
client. The API project still holds the `WeatherForecast` template, and the
first feature (#45) is about to add real endpoints. Every later endpoint
copies the first ones, so the conventions must be settled now.

Forces:

- **One client, generated.** The only client is our SPA. Its TypeScript
  client is generated from the OpenAPI document (ADR-0002, #17). Whatever the
  endpoints declare, including error and warning shapes, ends up as types in
  the frontend. Undeclared responses become `any`.
- **Errors already exist as exceptions.** The validation decorator throws
  `ValidationException` with every error before the handler runs (ADR-0011,
  #26). ADR-0015 adds `ForbiddenException` from domain checks,
  `UnregisteredUserException` for identities without a user, and a not-found
  error for anything outside the caller's facilities. ADR-0016 adds domain
  errors for a deleted comment and a deleted thread. They all need an HTTP
  status and a body the frontend can act on.
- **Some invariants are unique indexes.** Exclusive nights (ADR-0005), linked
  identities (ADR-0015) and the comment version chain (ADR-0016) are each
  enforced by a unique index. A handler checks first, but two requests at the
  same time can both pass the check. The loser gets SQL Server error 2627 or
  2601, which today surfaces as a 500.
- **Success can come with a warning.** A stay that pushes a night over the
  facility's capacity is saved, and the API returns a warning that the
  frontend shows (ADR-0005). It is the first of its kind, not the last.
- **Commands can run twice.** A client that times out, or a user who taps
  twice, sends the same command again. Making commands idempotent is #50.
  It needs a client-facing contract for idempotency keys, which this ADR
  defines. `UnitOfWork` saves and commits each command in one transaction
  (ADR-0014), so anything recorded about a request in that transaction exists
  if, and only if, the command committed.
- **Validation lives in Application.** ADR-0011 puts input rules in
  validators next to their command. A second set of rules in the API would
  drift.
- **The frontend will be translated.** The SPA is planned to support more
  than one language. Everything it shows a user, warnings and errors
  included, must come from its own translations, not from English text sent
  by the server. Validation errors (#26) carry only an English message
  today.
- **Cross-origin.** The SPA and the API are on different origins (ADR-0006,
  ADR-0015), so any response header the SPA reads, and any request header it
  sends, has to be allowed by CORS.
- **Licensing.** ADR-0011 avoided a mediator library because the leading
  one went commercial. The same author's mapping library did the same.
- **Solo developer.** Every convention must be cheap to follow and easy to
  check in review.

## Decision

We will write endpoints as **minimal APIs grouped per feature**, with
hand-written request and response contracts in the API project. Failures are
**exceptions mapped in one place to RFC 9457 problem details**. Every command
response carries a **`warnings` array**. Every `POST` requires an
**`Idempotency-Key`** header, and a repeated request gets the original
response back.

### Endpoints

- Endpoints are **minimal APIs**. Controllers and `AddControllers()` are
  removed together with the `WeatherForecast` template.
- The API project is organised by feature, like Application
  (`Facilities/`, `Bookings/`, ...). Each feature has a static class that
  maps its endpoints, for example `StayEndpoints.MapStayEndpoints(this
  IEndpointRouteBuilder)`, called once from `Program.cs`. A feature's routes
  share a `MapGroup` and an OpenAPI tag.
- Each endpoint is a **named static method**, not a lambda. Its parameters
  are the route values, the request contract, the handler interface it calls
  (ADR-0011) and the `CancellationToken`. The method maps the request to the
  command or query, calls the handler, and maps the result to a response. It
  holds no business logic.
- Endpoints return `TypedResults` with a `Results<...>` return type, so
  OpenAPI learns every success response from the signature. Every endpoint
  also declares the problem responses it can return.
- Each endpoint's OpenAPI operation id (`WithName`) is the name of the
  command or query it calls, for example `CreateStay`. The generated client
  then has the same vocabulary as the Application project.

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

    private static async Task<Created<CreateStayResponse>> CreateStay(
        Guid facilityId,
        CreateStayRequest request,
        ICommandHandler<Application.Bookings.CreateStay, Outcome<StayId>> handler,
        CancellationToken cancellationToken)
    {
        var outcome = await handler.HandleAsync(request.ToCommand(facilityId), cancellationToken);
        return TypedResults.Created($"/stays/{outcome.Value}", CreateStayResponse.From(outcome));
    }
}
```

### Routes and methods

- Paths are lower-case, plural nouns in kebab-case. There is no `/api`
  prefix and no version segment. The SPA is the only client and ships with a
  client generated from the same build, so a breaking change ships with the
  client that handles it.
- A collection that belongs to a facility is reached through it:
  `GET /facilities/{facilityId}/stays`, `POST /facilities/{facilityId}/stays`.
  A single resource is reached by its own id, without its parents:
  `GET /stays/{stayId}`. Commands addressed by a stay or comment id do not
  carry a facility id (ADR-0015), so neither do their routes. Things owned by
  a resource hang off it: `/stays/{stayId}/comments/{commentId}`.
- Each command maps to one method:
  - creating something is `POST` on the collection, answered with
    `201 Created` and a `Location` header;
  - deleting is `DELETE` on the resource;
  - a command that sets values given in full, and is idempotent by nature,
    is `PUT` on the resource or the part it sets, for example editing a
    comment's body;
  - any other command is `POST` on a named action:
    `POST /stays/{stayId}/confirm`.
- There is no `PATCH`. Commands are use cases (ADR-0011), not partial
  updates of a document.
- Queries are `GET`. Filters are query string parameters.
- Secrets never go in a URL, because URLs end up in logs and browser
  history. An invitation token is sent in the body of
  `POST /invitations/accept`, not in its path.
- The health probes (ADR-0007) and the OpenAPI document sit outside these
  conventions.

### Contracts

- Request and response contracts are **records in the API project**, next to
  the endpoints of their feature. Commands, queries and read models never
  appear in an endpoint signature or in the OpenAPI document. Application can
  change without changing the wire format, and the reverse.
- Mapping is **written by hand**: a `ToCommand(...)` method on the request and
  a static `From(...)` on the response. There is no mapping library.
- A request property has the same name as the command property it feeds,
  so validation errors (below) point at the field the client sent.
- On the wire:
  - property names are camelCase, and every declared property is always
    present, `null` when it has no value;
  - enums are camelCase strings;
  - dates without a time, such as nights, are `yyyy-MM-dd`;
  - points in time are ISO 8601 in UTC with a `Z`;
  - ids are strings, whatever type they have inside the application.
- Built-in minimal API validation and data annotations are not used.
  Validation stays in Application validators. The API only rejects a body it
  cannot parse or a route value of the wrong type, as a
  `malformed-request` problem.

### Command responses and warnings

- Every command response has a JSON body with a `warnings` array. It is
  always present, and empty when there is nothing to warn about. A command
  that creates something answers `201 Created` with `id` and `warnings`;
  every other command answers `200 OK` with `warnings` and whatever outcome
  the command returns. Command endpoints never answer `204 No Content`.
  Adding a warning to a command later therefore never changes its contract.
- A warning means the command **succeeded and was committed**. It is
  information for the user, not a question. The frontend shows it, and if
  the user disagrees they change or undo what they did with a normal
  command. For ADR-0005's capacity warning, the stay is saved and the
  frontend shows which nights are over capacity, offering to change or
  delete the stay.
- In Application, a command that can warn returns `Outcome<T>`: its result
  plus a list of `Warning`s. `Warning` is an abstract record with a `Code`;
  each kind of warning is a subtype carrying its own data, for example
  `CapacityExceededWarning(IReadOnlyList<DateOnly> Nights, int Capacity)`.
  Commands that cannot warn return their result as before, and their
  endpoints send `"warnings": []`.
- On the wire a warning has a `code` (stable, kebab-case), a `message` in
  English for developers and logs, and the fields of its kind. The response
  contracts are polymorphic on `code`, so OpenAPI and the generated client
  see a union of known warnings.

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

### Errors

- Every error response is an **RFC 9457 problem details** document with
  content type `application/problem+json`, written by ASP.NET Core's
  `AddProblemDetails()`. No endpoint builds an error body itself.
- Handlers and domain code **throw**; they do not return error results. A
  single `IExceptionHandler` in the API maps a closed list of exception types
  to problems. Anything not on the list is a `500`.
- Every problem has:
  - `type`: a link to the problem's entry in the API README;
  - `title`: fixed per type;
  - `status`;
  - `detail`: English, for developers, never shown to users as is, and never
    quoting user input (it ends up in logs, like the exception messages in
    #26);
  - `code`: the kebab-case name of the problem type. This is what the
    frontend switches on, and its translation key (see
    [Localisation](#localisation));
  - `traceId`: the request's trace id, so a user's report can be matched to
    the telemetry (ADR-0007);
  - any fields specific to the type.
- Problem and warning codes share one catalogue in the API README.

| Situation | Raised as | Status | `code` |
|---|---|---|---|
| Body cannot be parsed, or a route value has the wrong type | ASP.NET Core `BadHttpRequestException` | 400 | `malformed-request` |
| A validator rejected the command or query | `ValidationException` (#26) | 400 | `validation-failed`, with `errors` |
| `Idempotency-Key` missing or malformed on a `POST` | API filter | 400 | `idempotency-key-missing`, `idempotency-key-invalid` |
| No valid token | JWT bearer handler | 401 | `unauthenticated` |
| Signed in, but the identity is not linked to a user (ADR-0015) | fallback policy / `UnregisteredUserException` | 403 | `user-not-registered` |
| A domain check refused the caller (ADR-0015) | `ForbiddenException` | 403 | `forbidden` |
| The target does not exist, or is outside the caller's facilities (ADR-0015) | `NotFoundException` | 404 | `not-found` |
| A domain rule refused the change given the current state, e.g. an exclusive stay overlaps, a comment is deleted (ADR-0016) | domain rule exception with its own code | 409 | the rule's code, e.g. `exclusive-stay-overlaps`, `comment-deleted` |
| A unique index rejected the change | translated from 2627/2601 by `UnitOfWork` | 409 | the index's rule code if known, else `conflict` |
| The first request with this idempotency key is still running | API filter | 409 | `idempotency-key-in-use`, with `Retry-After` |
| The idempotency key was used before for a different request | API filter | 422 | `idempotency-key-reused` |
| Anything else | unhandled exception | 500 | `internal-error`, with no exception details outside Development |

- **Validation errors** have an `errors` object keyed by camelCase property
  name. Each property has a list of errors, and each error has a `code`, a
  `message` in English and the `parameters` its text needs. This replaces
  the plain list of messages in ASP.NET Core's `ValidationProblemDetails`,
  which cannot be translated. `ValidationError` (#26) gains the same `Code`
  and `Parameters`:

  ```csharp
  public sealed record ValidationError(
      string PropertyName,
      string Code,
      string Message,
      IReadOnlyDictionary<string, object>? Parameters = null);
  ```

  Validation codes are a small shared set, such as `required`, `too-long`,
  `out-of-range` and `must-be-after`, reused across fields, so the frontend
  translates a handful of codes rather than one per field.

  ```json
  "errors": {
    "body": [
      {
        "code": "too-long",
        "message": "Must be at most 4000 characters.",
        "parameters": { "maxLength": 4000 }
      }
    ]
  }
  ```

  Rules that depend only on the input, such as a comment body's length
  (ADR-0016), are checked by a validator so the client gets a `400` with
  field errors. Domain code checks them again as invariants, but a client
  should never reach that check.
- **Domain rule violations are 409, not 422.** They are refusals because of
  the state of what the request touches, which is what `409 Conflict` means.
  `422` is kept for a reused idempotency key, as the IETF draft specifies.
  Domain defines one base exception for rule violations that carries the
  rule's code. Each rule's code goes in the catalogue.
- **Unique-index violations become 409.** `UnitOfWork`, the only caller of
  `SaveChanges` (ADR-0014), catches a `DbUpdateException` caused by SQL
  Server error 2627 or 2601 and throws an Application `ConflictException`
  naming the index. These errors are not transient, so the execution
  strategy does not retry them. Infrastructure keeps a short map from index
  names to rule codes, so a race on exclusive nights gives the same
  `exclusive-stay-overlaps` problem as the handler's own check. An index not
  in the map gives a generic `conflict`, telling the user to reload and try
  again. The SQL Server error numbers stay in Infrastructure (ADR-0007).
- A request cancelled by the client is not an error. It is logged at
  information level and gets no problem body.

### Localisation

- The server sends **no text meant for users**. It does not read
  `Accept-Language` and has no resource files. Every `message`, `title` and
  `detail` is English, for developers and logs. The SPA never shows them.
- **The `code` is the translation key** for problems, warnings and
  validation errors. The server sends only the code. The frontend decides
  where it lives in its own translation files, for example
  `problems.exclusive-stay-overlaps`, so restructuring them never changes
  the API.
- **Parameters are typed values, not formatted text.** A warning or problem
  sends the values its sentence needs as fields: dates as `yyyy-MM-dd`,
  counts and limits as numbers. The frontend formats dates for the user's
  locale and picks plural forms itself, for example with ICU MessageFormat.
  The server never puts a formatted value into a message the user could
  see.
- **Unknown codes fall back by status.** An SPA build that meets a code it
  does not know shows a generic message for the status, for example "This
  was changed in the meantime. Reload and try again." for a `409`. It never
  falls back to `detail` or `message`.
- What users write, such as comment bodies and display names, is shown as
  written and never translated.

### Idempotency keys

This is the client-facing contract. Storing keys and responses, and
checking commits, is implemented in #50.

- **Header.** `Idempotency-Key`, as in the IETF HTTP API working group
  draft. The value is an opaque string of 1 to 64 printable ASCII
  characters. The SPA sends a new UUID per user action, and reuses it for
  every retry of that action.
- **Where.** Every `POST` endpoint **requires** it, and answers `400
  idempotency-key-missing` without one. `PUT` and `DELETE` are idempotent by
  definition (ADR-0016 already makes a repeated delete harmless), and `GET`
  changes nothing, so they ignore the header. One rule for every `POST`
  means no endpoint can forget it, and a double tap can never create two
  stays or two comments.
- **Scope.** A key belongs to the caller: the `UserId`, or for
  `AcceptInvitation`, the provider identity (ADR-0015). The same key from
  two callers is two keys.
- **Fingerprint.** With the key, the server remembers the method, the route
  and a SHA-256 hash of the raw body. The generated client always serialises
  a request the same way, so a true retry has the same fingerprint.
- **Replay.** A request whose key and fingerprint match a **committed**
  command gets the original response back: the same status code, the same
  body including its `warnings`, and the same `Location` header. It is
  indistinguishable from the first response, and the handler does not run
  again. Warnings are not recomputed, so the user sees what was true when
  the command ran.
- **Only committed commands are remembered.** The key and the response are
  written in the same transaction as the command's changes. A request that
  failed, whether with a `400`, a `409` or a `500`, left no record. A retry
  with the same key runs the command again and may get a different answer.
  A client that changes the request after an error must use a new key.
- **Reuse with a different request.** A key that matches a committed
  command but not its fingerprint gets `422 idempotency-key-reused`. The
  command does not run.
- **In flight.** If the first request with a key has not finished, the
  second gets `409 idempotency-key-in-use` with `Retry-After`, or the server
  waits for the first and replays its response. Which of the two happens is
  up to #50. A client handles both by retrying with the same key.
- **Lifetime.** A key is remembered for **at least 24 hours** after its
  command committed. After that, the same key starts a new command.

### OpenAPI

- The document is generated by the built-in `Microsoft.AspNetCore.OpenApi`
  package. It is served at `/openapi/v1.json` in every environment,
  anonymously (ADR-0015), and also written at build time for the client
  generator (#17).
- Every endpoint's problem responses use the problem details schema with
  the `code` and `traceId` fields. Each `POST` operation declares the
  required `Idempotency-Key` header.
- Problem codes and validation codes are declared as **enums** in the
  document, and warnings as a union on `code`. The generated client
  therefore has a type for each set of codes, and a translation table typed
  as, for example, `Record<ProblemCode, string>` does not compile until
  every code has a translation.

### Documentation

`FacilityManagementService.API/README.md` describes these conventions with
examples, in the same way as the Application README, and holds the
catalogue of problem and warning codes. Each problem's `type` links to its
entry there.

## Options considered

### Endpoint style

#### Option 1: Minimal APIs grouped per feature (chosen)

- Pros: Each endpoint declares the handler it calls in its own parameter
  list, which is what ADR-0011 asks for; `TypedResults` give OpenAPI the
  response types without attributes; route groups give per-feature
  structure; where most new ASP.NET Core features land; no extra package.
- Cons: Grouping and naming are conventions rather than something the
  framework enforces; less familiar to reviewers used to MVC; endpoint
  classes need care not to grow into one large file per feature.

#### Option 2: Controllers

- Pros: Familiar; one class per resource gives structure for free; mature
  filters and conventions.
- Cons: Handlers are injected through the constructor for all actions, or
  with `[FromServices]` per action, so the "one endpoint, one handler" link
  from ADR-0011 is weaker; response types need attributes to reach OpenAPI;
  MVC brings features (views, model validation) we would turn off.

#### Option 3: An endpoint library (FastEndpoints, Carter)

- Pros: One class per endpoint (the REPR pattern) out of the box;
  conventions enforced by the library.
- Cons: A third-party dependency in front of every use case, which ADR-0011
  avoided for the dispatcher; FastEndpoints brings its own validation and
  messaging, which overlap with ADR-0011's handlers and decorators; the
  structure it adds is small enough to keep by convention.

### Contracts

#### Option 1: API-owned request and response records, mapped by hand (chosen)

- Pros: What ADR-0003 already places in the API; the wire format and
  Application change independently; route values, the current user and the
  body come together in one obvious place; mapping is plain code that "go to
  definition" can follow.
- Cons: Each command and read model has a near-copy in the API, plus
  mapping code; renaming a field means touching both.

#### Option 2: Bind commands and return read models directly

- Pros: No duplicates and no mapping.
- Cons: Application types become the public contract, so refactoring a
  command breaks the generated client; route values and body properties
  collide in the same record; strongly typed ids and domain types leak into
  the OpenAPI document; contradicts ADR-0003.

#### Option 3: A mapping library

- Pros: Less hand-written mapping.
- Cons: The best-known library went commercial under the same terms as
  MediatR (ADR-0011); mapping by convention hides mistakes until runtime;
  the mappings here are short.

### How errors reach the API

#### Option 1: Exceptions mapped centrally to problem details (chosen)

- Pros: Matches what exists: the validation decorator, `ForbiddenException`
  and the ADR-0015 and ADR-0016 errors are already exceptions; one place
  decides every status code; handlers stay focused on the success path; the
  response format is a standard that ASP.NET Core and the generated client
  both understand.
- Cons: Exceptions as control flow; which exceptions a handler can throw is
  not in its signature, so the catalogue and endpoint metadata must be kept
  up to date by hand.

#### Option 2: Handlers return a result type with errors

- Pros: Failures are visible in signatures; no exceptions for expected
  outcomes.
- Cons: Every handler, decorator and endpoint has to unwrap results; the
  existing decorators and ADR-0015/0016 would change; domain code that
  throws on a broken invariant would still need a second path.

#### Option 3: Our own error body

- Pros: Shaped exactly as we like.
- Cons: Reinvents RFC 9457; ASP.NET Core's built-in errors (authentication,
  malformed requests) would come out in a different format unless
  rewritten.

### Status for refused domain rules

#### Option 1: 409 Conflict (chosen)

- Pros: The refusal is about the current state, which is what 409 means;
  the same status for the handler's check and for a unique index catching
  the same rule in a race.
- Cons: Some APIs use 422 for business rules, so readers may expect it.

#### Option 2: 422 Unprocessable Content

- Pros: Common for "valid request that breaks a rule".
- Cons: The idempotency draft uses 422 for a reused key, so the frontend
  would have to tell two different meanings apart.

#### Option 3: 400 Bad Request

- Pros: One status for every client error.
- Cons: The client cannot tell "fix your input" from "the world changed".

### Unique-index violations

#### Option 1: Translate to 409 in `UnitOfWork` (chosen)

- Pros: A race gets the same answer as the checked case; translation lives
  in the one class that saves; SQL Server error numbers stay in
  Infrastructure.
- Cons: A map from index names to rule codes to maintain; before #50 lands,
  a retried commit whose first attempt succeeded also gets a 409 rather
  than its result.

#### Option 2: Leave them as 500

- Pros: Nothing to build.
- Cons: A normal race looks like a server fault to the user and in the
  telemetry.

### Warnings

#### Option 1: A `warnings` array in every command response (chosen)

- Pros: Adding a warning to a command never changes its contract; one
  pattern for the frontend; warnings are part of the body, so a replayed
  response includes them for free.
- Cons: Commands that will never warn still send `"warnings": []`; no
  command endpoint can use `204`.

#### Option 2: `warnings` only on commands that can warn

- Pros: Smaller responses; `204` stays available.
- Cons: The first warning on an existing command changes its response type
  and the generated client.

#### Option 3: Refuse until the client acknowledges the warning

The first request fails with the warning; the client resends it with an
acknowledgement flag.

- Pros: The user confirms before anything is saved.
- Cons: Not "succeeded with warnings" at all: a warning becomes an error
  with a bypass; two round trips; ADR-0005 says the stay is saved.

#### Option 4: A response header

- Pros: The body is unchanged.
- Cons: The HTTP `Warning` header is obsolete (RFC 9111); headers carry
  structured data poorly and need CORS exposure; a header is easy to drop
  when replaying.

### Text for users

#### Option 1: Codes with typed parameters, translated by the frontend (chosen)

- Pros: The API stays language-neutral; adding a language touches only the
  frontend; dates and plurals follow the user's locale; codes in the
  OpenAPI document let the compiler find missing translations.
- Cons: Every new code needs a translation before it reads well; the
  server's English messages are never seen by users, so they serve only
  developers.

#### Option 2: The server translates, chosen by `Accept-Language`

- Pros: The frontend shows what it receives; one place for all text.
- Cons: Translations split between backend resource files and the
  frontend's own UI text; the server must format dates and plurals for
  every locale; a replayed response (see idempotency keys) would come back
  in the language of the first request.

#### Option 3: The server sends the frontend's translation keys

- Pros: The frontend looks keys up directly.
- Cons: The API depends on how the frontend organises its translation
  files; renaming a key there is a breaking API change.

### Idempotency key requirement

#### Option 1: Required on every `POST` (chosen)

- Pros: One rule, checked by one filter; no endpoint can forget it; the
  only client is ours and generated, so sending it costs nothing.
- Cons: Manual requests (`.http` files, curl) need a header; `POST`
  actions that happen to be idempotent require it anyway.

#### Option 2: Optional

- Pros: Easier to call by hand.
- Cons: The protection depends on every caller remembering it; a bug in
  the client silently brings back duplicate stays.

#### Option 3: Required only where a `POST` creates something

- Pros: The key is only asked for where duplicates do harm.
- Cons: Someone has to decide per endpoint, and get it right; a second
  rule to remember.

### Which outcomes a key remembers

#### Option 1: Only committed commands (chosen)

- Pros: Falls out of writing the key in the command's own transaction; a
  key means exactly "this command committed"; a retry after a transient
  error just works.
- Cons: A retry after a `4xx` runs the command again rather than repeating
  the error, so a client may see different errors for the same key.

#### Option 2: Every response, errors included

- Pros: A repeated request always gets exactly the same answer.
- Cons: Errors would have to be stored outside the rolled-back
  transaction; a retry after a transient `500` replays the `500` forever.

## Consequences

### Positive

- One shape for success, warnings and errors across every feature, declared
  in OpenAPI, so the generated client has types for all of them.
- The frontend switches on stable codes and owns all user-facing text, so
  adding a language never touches the API. Missing translations are found
  by the compiler.
- A race on a unique index looks like the rule it protects, not like a
  crash.
- A double tap or a retried request cannot create something twice, and the
  user sees the same result, warnings included.
- Each endpoint shows its route, contract and handler in one method, and
  "go to definition" leads from there to the use case.

### Negative

- Every command and read model has a hand-written contract and mapping in
  the API.
- The list of exceptions, codes and the problem responses each endpoint
  declares are kept in step by hand. A test should check that every code
  the exception handler can produce is in the catalogue.
- CORS must allow the `Idempotency-Key` request header and expose the
  `Location` and `Retry-After` response headers to the SPA's origin.
- Testing a `POST` by hand needs an `Idempotency-Key` header.
- Every validator must give each error a code from the shared set, and
  every new problem or warning code needs a frontend translation before it
  ships. Validation errors no longer match the shape of ASP.NET Core's
  `ValidationProblemDetails`.
- Without a version in the URL, a browser tab with an old SPA build can
  call a changed endpoint. Breaking changes should be rare and are caught
  by the generated client's types at build time.
- Until #50 lands, the key is required but not yet honoured, and a commit
  that succeeded and was retried can still answer `409`.

### Follow-up

- Replace the controllers and the `WeatherForecast` template with minimal
  APIs, add the exception handler and problem details setup, the
  `malformed-request`, `unauthenticated` and `internal-error` problems, the
  `Outcome<T>` and `Warning` types, `Code` and `Parameters` on
  `ValidationError` with the shared validation codes, the code enums in the
  OpenAPI document, the shared command response, the
  `Idempotency-Key` presence check, the translation of 2627/2601 in
  `UnitOfWork`, and the API README with its catalogue
  ([#52](https://github.com/gyback/facility-management/issues/52)).
- Store and replay idempotency keys as specified here
  ([#50](https://github.com/gyback/facility-management/issues/50)).
- Map `UnregisteredUserException` and `ForbiddenException`, and add
  `NotFoundException`, with the authentication work
  ([#41](https://github.com/gyback/facility-management/issues/41)) and the
  facilities feature
  ([#45](https://github.com/gyback/facility-management/issues/45)).
- Add the capacity warning, and the codes for the exclusive-stay and
  deleted-comment rules, with the booking feature
  ([#8](https://github.com/gyback/facility-management/issues/8)).
- Write the OpenAPI document at build time and set operation ids for the
  client generator
  ([#17](https://github.com/gyback/facility-management/issues/17)).

## References

- Issue [#43: Decide the API endpoint style and error response conventions](https://github.com/gyback/facility-management/issues/43)
- Issue [#50: Make commands idempotent](https://github.com/gyback/facility-management/issues/50)
- [ADR-0002: Use C# and .NET for the backend](0002-use-csharp-and-dotnet-for-the-backend.md)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0011: Use hand-rolled command and query handlers](0011-use-hand-rolled-command-and-query-handlers.md)
- [ADR-0014: Use aggregate repositories for commands and projections for queries](0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md)
- [ADR-0015: Authenticate with Entra External ID and link identities to users by invitation](0015-authenticate-with-entra-external-id-and-invitation-linked-users.md)
- [ADR-0016: Keep the history of edited and deleted comments](0016-keep-the-history-of-edited-and-deleted-comments.md)
- IETF, [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457)
- IETF, [The Idempotency-Key HTTP Header Field](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)
- IETF, [RFC 9111, section 5.5: Warning (obsoleted)](https://www.rfc-editor.org/rfc/rfc9111#section-5.5)
- Microsoft, [Minimal APIs overview](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview)
- Microsoft, [Handle errors in ASP.NET Core APIs](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api)
