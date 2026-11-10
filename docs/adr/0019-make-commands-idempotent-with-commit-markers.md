# ADR-0019: Make commands idempotent with commit markers

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** Gustav Gybäck (sole developer)

## Context

`UnitOfWork` wraps every command in EF Core's retrying execution strategy
(ADR-0008, ADR-0011, ADR-0014, #40), and ADR-0017 defines the client-facing
`Idempotency-Key` contract for `POST` requests (#43). Two forms of duplicate
command execution remain open:

1. **Lost commit acknowledgement inside `UnitOfWork`.** When a transient
   failure occurs during `DbTransaction.CommitAsync`, SQL Server may already
   have committed the transaction before the connection dropped. If the
   execution strategy simply re-runs the operation, the handler runs a second
   time: it either creates a duplicate aggregate with a new domain-generated
   version 7 GUID (ADR-0018) or trips a unique index and surfaces a spurious
   `409 Conflict` (ADR-0017). EF Core's `IExecutionStrategy.ExecuteAsync`
   accepts a `verifySucceeded` callback for exactly this case, which #40 left
   `null` until commands were made idempotent.
2. **Repeated `POST` requests at the API boundary.** A client whose connection
   drops before the response arrives, or a user who taps twice, sends the same
   `POST` again with the same `Idempotency-Key` (ADR-0017). Today
   `IdempotencyKeyMiddleware` (#52) only validates that the header is present
   and well-formed; it does not store or replay committed commands.

Forces:

- **One question at both levels.** Both commit verification and API
  idempotency ask whether a specific command already committed. Because
  `UnitOfWork` saves and commits each command in a single transaction
  (ADR-0014), a marker row inserted in that transaction exists if, and only
  if, the command committed.
- **Not every command has an `Idempotency-Key`.** `PUT` and `DELETE` endpoints
  ignore `Idempotency-Key` (ADR-0017), and tests or internal callers invoke
  `IUnitOfWork` without an HTTP request, yet `UnitOfWork` still needs a marker
  to verify whether an ambiguous `CommitAsync` succeeded.
- **Endpoints are deterministic over `TResult`.** `UnitOfWork` sits in
  Infrastructure behind Application's `IUnitOfWork` (ADR-0003, ADR-0011) and
  sees the handler's return value `TResult` (such as `FacilityId`,
  `CreatedFacilityInvitation`, `AcceptedInvitation`, `Outcome<T>` or `Unit`),
  not the HTTP response. Every command endpoint maps `TResult` directly to its
  `TypedResults` response (`201 Created` with `Location`, or `200 OK`, with
  `warnings`) with no other side effects (ADR-0017).
- **Caller scope across invitation acceptance.** ADR-0017 scopes an
  `Idempotency-Key` to the caller: `UserId` on normal endpoints, or the
  external identity on `POST /invitations/accept` (ADR-0015). Because
  accepting an invitation links the external identity to a user on the first
  request, a retry of `POST /invitations/accept` arrives after the identity is
  linked. The key's scope must depend on the endpoint's authorization policy
  rather than whether the caller currently has a `UserId`, so the first call
  and its retry compute the same scope.
- **Engine neutrality.** Migrations must use only standard EF Core model
  constructs with no engine-specific SQL such as filtered unique index
  predicates (ADR-0007, ADR-0009).
- **Scale-to-zero hosting.** The API and Azure SQL Serverless are sized to
  pause when idle (ADR-0006, ADR-0008). A background timer that polls the
  database every few minutes to purge old markers would keep both awake.
- **Solo developer.** One mechanism and one table are cheaper to maintain than
  two (ADR-0003).

## Decision

We will record a **`CommandMarker` row inside every command's transaction** in
`UnitOfWork`, using a single `CommandMarkers` table for both EF Core commit
verification and HTTP `POST` idempotency replay.

### The `CommandMarkers` table

Each committed command writes one row to `CommandMarkers`:

| Column | Type | Purpose |
|---|---|---|
| `Scope` | `nvarchar(360)`, `NOT NULL` | Partition of `Key`: `user:{UserId}`, `identity:{Provider}:{Subject}`, or `commit` |
| `Key` | `nvarchar(64)`, `NOT NULL` | Client `Idempotency-Key` on `POST`, or a per-attempt version 7 GUID when no key applies |
| `Fingerprint` | `nvarchar(64)`, `NULL` | Lowercase hex SHA-256 of the HTTP method, path, query string and raw body (`NULL` for `commit` scope) |
| `ResultJson` | `nvarchar(max)`, `NULL` | JSON-serialised handler result `TResult` (`NULL` for `commit` scope) |
| `CreatedAt` | `datetimeoffset`, `NOT NULL` | UTC timestamp when the command committed |
| `ExpiresAt` | `datetimeoffset`, `NOT NULL` | UTC timestamp after which the marker may be deleted and its key reused |

- The primary key is the composite `(Scope, Key)` (`PK_CommandMarkers`), and
  `ExpiresAt` is indexed (`IX_CommandMarkers_ExpiresAt`). Because both key
  columns are non-null for every marker, uniqueness needs no filtered index
  and generates engine-neutral migrations (ADR-0007, ADR-0009).
- When a command runs under an HTTP `POST` with an `Idempotency-Key`, `Scope`
  and `Key` come from the request, `Fingerprint` and `ResultJson` are
  populated, and `ExpiresAt` is `CreatedAt + 24 hours` (ADR-0017).
- When a command has no `Idempotency-Key` (`PUT`, `DELETE`, or a non-HTTP
  caller), `Scope` is `"commit"`, `Key` is a fresh version 7 GUID generated per
  attempt, `Fingerprint` and `ResultJson` are `null`, and `ExpiresAt` is
  `CreatedAt + 5 minutes` (long enough to outlast any execution-strategy retry
  window).

### Commit verification in `UnitOfWork`

- `UnitOfWork.ExecuteAsync<TResult>` calls the `IExecutionStrategy.ExecuteAsync`
  overload that accepts both `operation` and `verifySucceeded`.
- On each attempt, `operation` clears the `ChangeTracker` if a previous
  attempt ran, opens a transaction, runs the handler delegate, stages the
  `CommandMarker` row, and calls `SaveChangesAsync`. Only after
  `SaveChangesAsync` succeeds does the attempt record its `(Scope, Key)` and
  `TResult` as a commit candidate right before calling
  `transaction.CommitAsync`.
- If a transient exception is thrown before `CommitAsync` is reached, the
  transaction was not committed; `verifySucceeded` returns `false` immediately
  without querying the database, and the execution strategy retries.
- If a transient exception is thrown during `CommitAsync`, `verifySucceeded`
  clears the `ChangeTracker` and queries `CommandMarkers` with `AsNoTracking`
  for `(Scope, Key)`. If the row exists, the commit succeeded despite the lost
  acknowledgement: `verifySucceeded` returns `true` together with the
  in-memory `TResult` captured before `CommitAsync`, so the handler does not
  run a second time. If the row does not exist, `verifySucceeded` returns
  `false`, and a non-idempotency command generates a fresh `Key` on the next
  attempt.

### API idempotency keys (`POST`)

- **Scope:** `IdempotencyKeyMiddleware` computes the caller scope after
  authentication and authorization:
  - Endpoints that opt into unregistered identities with
    `.AllowUnregisteredUser()` (`POST /invitations/accept`, ADR-0015) always
    use `identity:{Provider}:{Subject}`, even if the identity has since been
    linked to a `UserId`.
  - All other authenticated endpoints use `user:{UserId}`.
- **Fingerprint:** `IdempotencyKeyMiddleware` enables request buffering, reads
  the raw body bytes, rewinds `Request.Body`, and hashes the method,
  path + query string, and body bytes with SHA-256.
- **In-flight requests (`409 idempotency-key-in-use`):** An in-memory
  `InFlightIdempotencyKeys` tracks active `(Scope, Key)` pairs for the duration
  of a `POST` request. If a second request with the same `(Scope, Key)`
  arrives while the first is still running, the middleware answers `409
  Conflict` with problem code `idempotency-key-in-use` and `Retry-After: 1`
  (ADR-0017). If two requests race across instances and one hits a unique-key
  violation on `PK_CommandMarkers` inside `UnitOfWork`, the middleware catches
  `ConflictException` for `PK_CommandMarkers`, checks the stored marker, and
  returns `422 idempotency-key-reused` if the fingerprint differs or `409
  idempotency-key-in-use` with `Retry-After: 1` otherwise.
- **Lookup before the handler runs:** After acquiring the in-flight slot, the
  middleware queries `IIdempotencyKeyLookup` for an unexpired marker matching
  `(Scope, Key)`:
  - If found with a **different `Fingerprint`**, the middleware returns `422
    Unprocessable Content` with problem code `idempotency-key-reused`
    (ADR-0017) without invoking the endpoint.
  - If found with the **same `Fingerprint`**, the middleware places the stored
    `ResultJson` into the scoped `IdempotencyContext` as a replay and invokes
    the endpoint.
  - If not found, the middleware places `(Scope, Key, Fingerprint)` into
    `IdempotencyContext` so `UnitOfWork` writes the marker in the command's
    transaction.
- **Replay through `UnitOfWork`:** When `IdempotencyContext` holds a replayed
  `ResultJson`, `UnitOfWork.ExecuteAsync<TResult>` deserialises `TResult` and
  returns it immediately without opening a transaction or calling the inner
  command handler. Validation and logging decorators still run cleanly, and
  the endpoint maps `TResult` to the exact same `TypedResults` status code,
  `Location` header and `warnings` array as the first call.
- **Failed commands leave no marker:** Because the marker is staged in the
  command's transaction, any failure before or during `SaveChangesAsync`
  (`ValidationException`, `NotFoundException`, `ForbiddenException`,
  `DomainRuleException`, `ConflictException`, or an unhandled exception) rolls
  back or never reaches `UnitOfWork`. A subsequent request with the same key
  runs the command afresh (ADR-0017).

### Marker cleanup

- Before staging a new `CommandMarker`, `UnitOfWork` deletes expired markers
  (`WHERE ExpiresAt <= now`) using EF Core's `ExecuteDeleteAsync` inside the
  command transaction.
- Deleting expired rows first ensures that if a client reuses an
  `Idempotency-Key` after its 24-hour lifetime has elapsed, the old row is
  removed before `SaveChangesAsync` inserts the new row with the same
  `(Scope, Key)`.
- No background timer runs when the application is idle, preserving
  scale-to-zero behaviour for both the container and Azure SQL Serverless
  (ADR-0006, ADR-0008).

## Options considered

### Storage layout for commit verification and idempotency keys

#### Option 1: One `CommandMarkers` table for both (chosen)

- Pros: One table, one entity configuration, and one cleanup query; for `POST`
  requests, a single row written in the command's transaction serves both
  `verifySucceeded` and later HTTP replays; non-nullable `(Scope, Key)` primary
  key avoids engine-specific filtered unique indexes (ADR-0007).
- Cons: Rows for `PUT`/`DELETE` (`Scope = "commit"`) leave `Fingerprint` and
  `ResultJson` as `NULL`.

#### Option 2: Separate `CommitMarkers` and `IdempotencyKeys` tables

- Pros: Each table has only non-null columns for its single purpose.
- Cons: Two tables, two configurations, two cleanup queries, and `POST`
  commands either write to both tables in every transaction or `UnitOfWork`
  has to branch between two entity types in `verifySucceeded`.

### What to store for replayed `POST` responses

#### Option 1: Serialised handler result `TResult` in `UnitOfWork` (chosen)

- Pros: Written naturally inside `UnitOfWork` where `TResult` is returned and
  the transaction lives; endpoints remain plain `TypedResults` methods with no
  response-stream buffering; `201 Created` `Location` headers and `Outcome<T>`
  warnings are reconstructed deterministically from `TResult` by the endpoint
  itself; `UnitOfWork` can also replay at the application boundary in
  integration tests.
- Cons: `TResult` types returned by commands (including `Outcome<T>` and
  polymorphic `Warning` subtypes) must be JSON-serialisable; a two-phase
  commit would be needed if an endpoint ever computed non-deterministic data
  outside the handler (which ADR-0017 forbids).

#### Option 2: Capture the raw HTTP status code, headers and body in middleware

- Pros: Works even if an endpoint adds data outside the handler.
- Cons: The HTTP response is produced *after* `UnitOfWork.ExecuteAsync` has
  already committed the transaction, so writing the response into the same
  transaction requires either moving transaction management into HTTP
  middleware (breaking ADR-0011 and ADR-0014) or a second database write after
  commit that can itself fail after the command committed.

### Concurrent in-flight requests with the same `Idempotency-Key`

#### Option 1: Reject the second request with `409 idempotency-key-in-use` and `Retry-After` (chosen)

- Pros: Simple, non-blocking, and explicitly allowed by ADR-0017 and the IETF
  `Idempotency-Key` draft; does not hold open threads or connections waiting
  for another request's transaction.
- Cons: A client that sends two identical requests concurrently receives `409`
  on the second and must honour `Retry-After`.

#### Option 2: Hold the second request until the first finishes and replay

- Pros: The second caller gets the final response without retrying.
- Cons: Coordinates waiters across requests (and only works within a single
  process unless backed by distributed locking); ties up request threads while
  a slow database transaction runs.

### How expired markers are cleaned up

#### Option 1: Inline `ExecuteDeleteAsync` during command execution in `UnitOfWork` (chosen)

- Pros: Zero background polling, so Azure SQL Serverless and the container can
  scale to zero when idle (ADR-0006, ADR-0008); guarantees an expired key is
  purged right before a new command with the same `(Scope, Key)` inserts its
  marker; indexed on `ExpiresAt`, so the delete is a fast index range operation
  at our expected volume (ADR-0009).
- Cons: Adds one `DELETE FROM CommandMarkers WHERE ExpiresAt <= @now` statement
  to each command transaction.

#### Option 2: Background `IHostedService` timer

- Pros: Keeps the `DELETE` out of the command transaction path.
- Cons: Periodic queries prevent Azure SQL Serverless from auto-pausing and
  violate ADR-0006/ADR-0008's no-idle-polling constraint; an expired key can
  still collide on `PK_CommandMarkers` between timer ticks unless the command
  path also deletes or overwrites expired rows.

## Consequences

### Positive

- A transient failure during `CommitAsync` whose transaction actually
  committed on the server no longer re-runs the handler or fails with a false
  `409 Conflict`; `UnitOfWork` verifies the marker and returns the first
  attempt's result.
- Retried `POST` requests with the same `Idempotency-Key` and payload return
  the original status code, `Location` header and `warnings` without running
  the handler again.
- Reusing an `Idempotency-Key` with a different method, URL or body is rejected
  with `422 idempotency-key-reused`, and concurrent duplicates are rejected
  with `409 idempotency-key-in-use` and `Retry-After: 1`.
- Expired markers are cleaned up on write without background timers or
  engine-specific SQL.

### Negative

- Every command transaction executes one extra `DELETE` (for expired markers)
  and inserts one `CommandMarker` row.
- Every command result type `TResult` (and any new `Warning` subtype) must be
  serialisable by `CommandResultSerializer`.
- In-flight detection via `InFlightIdempotencyKeys` is per-process; if the app
  ever scales out to multiple replicas, two simultaneous requests with the
  same key on different replicas can both run until `SaveChangesAsync`, where
  `PK_CommandMarkers` rejects the loser and the middleware translates that
  unique-key conflict into `409 idempotency-key-in-use` (or `422
  idempotency-key-reused`).

### Follow-up

- Implement `CommandMarker`, the EF Core migration, commit verification and
  cleanup in `UnitOfWork`, and `POST` idempotency-key storage and replay in
  the API
  ([#50](https://github.com/gyback/facility-management/issues/50)).

## References

- Issue [#50: Make commands idempotent: API idempotency keys and commit verification](https://github.com/gyback/facility-management/issues/50)
- Issue [#40: Implement the persistence foundation in Application and Infrastructure](https://github.com/gyback/facility-management/issues/40)
- Issue [#43: Decide the API endpoint style and error response conventions](https://github.com/gyback/facility-management/issues/43)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [ADR-0011: Use hand-rolled command and query handlers](0011-use-hand-rolled-command-and-query-handlers.md)
- [ADR-0014: Use aggregate repositories for commands and projections for queries](0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md)
- [ADR-0015: Authenticate with Entra External ID and link identities to users by invitation](0015-authenticate-with-entra-external-id-and-invitation-linked-users.md)
- [ADR-0017: Use minimal APIs with problem details, warnings and idempotency keys](0017-use-minimal-apis-with-problem-details-warnings-and-idempotency-keys.md)
- [ADR-0018: Use domain-generated version 7 GUIDs as strongly typed IDs](0018-use-domain-generated-version-7-guids-as-strongly-typed-ids.md)
- IETF, [The Idempotency-Key HTTP Header Field](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)
- Microsoft, [Connection resiliency and execution strategies — Transaction commit failure and the idempotency issue](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#transaction-commit-failure-and-the-idempotency-issue)
