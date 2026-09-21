# ADR-0005: Use PostgreSQL

- **Status:** Proposed
- **Date:** 2026-09-21
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0004 selects a relational database. The candidates realistically
available to a .NET application running in Docker on a homelab are
PostgreSQL, Microsoft SQL Server, MySQL/MariaDB and SQLite.

Constraints and forces:

- The database runs as a container next to the API on a single VM host, so
  it must have an official, lightweight Linux image and be free to run
  without licensing concerns.
- Two invariants would ideally be enforced by the engine: no overlapping
  bookings per facility, and a consistent, auditable expense ledger.
- Instruction content and comment bodies are semi-structured; a good JSON
  column type is useful.
- The developer is one person; strong community documentation and
  first-class Entity Framework Core support reduce friction.
- Backups must be simple to automate to an off-host location.

## Decision

We will use PostgreSQL as the database engine, accessed from .NET through the
Npgsql provider.

PostgreSQL is free and open source, has excellent Npgsql and EF Core support,
a large community, and extensibility that the domain benefits from directly:
range types with exclusion constraints make double-booking impossible at the
database level, `jsonb` handles semi-structured content, and `pg_dump` gives a
trivial, portable backup story.

## Options considered

### Option 1: PostgreSQL

- Pros: Free, open source, no edition limits; range types and
  `EXCLUDE USING gist` for booking overlap; `jsonb` with indexing; mature
  Npgsql EF Core provider; official Docker image; `pg_dump`/`pg_restore`
  for backups; very widely documented.
- Cons: Case-sensitive identifiers and naming conventions differ from the
  .NET default (PascalCase), requiring a naming convention plugin or
  accepting quoted identifiers; slightly less integrated tooling than SQL
  Server inside the Microsoft ecosystem.

### Option 2: Microsoft SQL Server (Express or Developer edition)

- Pros: Tightest integration with .NET and EF Core; familiar to most C#
  developers; SSMS tooling.
- Cons: Express edition has 10 GB database and CPU/RAM limits; Developer
  edition is not licensed for production use even at home; Linux container
  image is large (over 1 GB); no range/exclusion constraints, so booking
  overlap must be enforced with triggers or application logic; heavier
  resource footprint on a homelab VM.

### Option 3: MySQL / MariaDB

- Pros: Free; lightweight; widely used.
- Cons: Weaker EF Core provider (Pomelo is community-maintained); no
  exclusion constraints or range types; historically laxer about data
  integrity defaults; JSON support less capable than `jsonb`.

### Option 4: SQLite

- Pros: Zero infrastructure, single file, trivial backups.
- Cons: Single-writer; limited ALTER TABLE support makes EF migrations
  painful; no exclusion constraints; fine for tests but a poor long-term
  system of record for a multi-user service.

## Consequences

### Positive

- Booking overlap can be enforced with a `tstzrange` exclusion constraint,
  independent of application code.
- Semi-structured content fits in `jsonb` columns without a second store.
- Backups are a scheduled `pg_dump` shipped off the host.
- No licence to think about as the number of users or facilities grows.

### Negative

- Need to settle a naming convention (snake_case via
  `EFCore.NamingConventions` is the common choice) early, since changing it
  later means a migration touching every table.
- Some PostgreSQL-specific features (exclusion constraints, `jsonb`
  operators) are not expressible in EF Core's provider-neutral model and will
  be configured through raw SQL in migrations, tying the schema to
  PostgreSQL. This is accepted; there is no plan to switch engines.

### Follow-up

- Add the `postgres` service to the Docker Compose stack (ADR-0007) with a
  named volume for data.
- Set up an automated `pg_dump` job that copies backups off the VM host.
- Use the exclusion constraint for bookings in the first migration that
  creates the bookings table.

## References

- [ADR-0004: Use a relational database](0004-use-a-relational-database.md)
- [ADR-0006: Use Entity Framework Core for data access](0006-use-entity-framework-core-for-data-access.md)
- [ADR-0007: Deploy with Docker Compose on the homelab](0007-deploy-with-docker-compose-on-the-homelab.md)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
- [PostgreSQL exclusion constraints](https://www.postgresql.org/docs/current/ddl-constraints.html#DDL-CONSTRAINTS-EXCLUSION)
