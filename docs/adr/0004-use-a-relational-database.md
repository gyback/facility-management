# ADR-0004: Use a relational database

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

The system's data is highly interconnected and is queried from many entry
points. The core entities and their relationships include:

- **Facilities** (cabins, houses) with many **owners/members** who hold roles
  per facility.
- **Stays** (bookings) belonging to a facility and a user, covering a set of
  nights, with comment threads.
- **Instructions** attached to a facility.
- **Maintenance schedules** per facility that generate concrete
  **occurrences**, against which **work log entries** are recorded.
- **Projects / renovations** per facility with **expenses**, each paid by one
  user and split across several owners according to a **split rule**,
  producing per-user **shares** and balances.

Typical questions cut across these relationships: "what does each owner owe
for cabin A this year?", "which recurring tasks are overdue across all my
facilities?", "who booked the cabin during the week that the roof leaked?".

Two hard integrity requirements stand out:

- The family's cabins are large and stays normally overlap, but a stay must
  occasionally be able to claim a facility exclusively, and that claim must
  be guaranteed rather than merely displayed. The exact booking model is a
  separate domain decision; whatever it is, it needs a uniqueness guarantee
  per facility and night.
- The expense ledger must be append-only and its shares must always sum to
  the expense amount, since it is the basis for settling money between
  relatives.

Both are expressible with unique indexes, foreign keys and transactions; no
engine-specific constraint type is required.

The data volume is tiny (a few facilities, tens of users, thousands of rows
over years), so scalability of writes or horizontal partitioning is not a
factor.

## Decision

We will use a relational (SQL) database as the single system of record.

The data is inherently relational, is accessed through many different joins,
and requires transactional integrity and database-level constraints
(uniqueness, foreign keys). These are exactly the strengths
of a relational database, and nothing in the workload argues for a document
or key-value model.

## Options considered

### Option 1: Relational database (SQL)

- Pros: Natural fit for many-to-many and hierarchical relationships; ad-hoc
  querying from any entry point via joins; ACID transactions across the
  ledger; constraints enforce invariants regardless of application bugs;
  mature migrations and tooling in .NET.
- Cons: Schema changes require migrations; unstructured content (e.g. rich
  instruction pages) needs a JSON column or a serialised blob.

### Option 2: Document database (e.g. MongoDB)

- Pros: Flexible schema; easy to store a whole facility document with nested
  instructions; simple to start.
- Cons: Cross-entity queries (per-owner balances across facilities, overdue
  tasks across facilities) require either denormalisation or application-side
  joins; multi-document transactions are possible but awkward; unique
  constraints across documents are limited, so the exclusive-night rule and
  ledger invariants live only in application code.

### Option 3: Hybrid (SQL for core, document store for content)

- Pros: Best tool for each kind of data.
- Cons: Two databases to run, back up and secure on a homelab for a workload
  that does not need it; a JSON column in a relational database covers the
  same need.

## Consequences

### Positive

- One database to operate and back up.
- Referential integrity and uniqueness constraints protect the data even if
  the application has bugs.
- Reporting and cross-facility views are straightforward SQL.

### Negative

- Every schema change is a migration that must be written, reviewed and
  applied in deployment.
- Semi-structured content (instruction pages, comment bodies) will be stored
  as text or JSON columns rather than as first-class documents.

### Follow-up

- Choose the specific database engine (ADR-0008).
- Choose the data access approach (ADR-0009).

## References

- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
