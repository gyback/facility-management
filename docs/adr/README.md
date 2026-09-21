# Architecture Decision Records

This folder contains the Architecture Decision Records (ADRs) for
FacilityManagementService. An ADR captures a single significant architectural
decision together with its context and consequences.

## When to write an ADR

Write an ADR when a decision:

- affects the structure, dependencies or interfaces of the system,
- is hard or expensive to reverse,
- chooses between several reasonable alternatives, or
- is likely to prompt "why did we do it this way?" later on.

## How to add a new ADR

1. Copy [`template.md`](template.md) to `NNNN-short-title-in-kebab-case.md`,
   using the next free number (zero-padded to four digits).
2. Fill in the sections and set the status to **Proposed**.
3. Open a PR; discuss and refine the ADR in review.
4. When merged, set the status to **Accepted** and add it to the index below.

ADRs are immutable once accepted. To change a decision, write a new ADR and mark
the old one as **Superseded by ADR-XXXX** (linking both ways).

## Statuses

| Status     | Meaning                                              |
|------------|------------------------------------------------------|
| Proposed   | Under discussion, not yet agreed.                    |
| Accepted   | Agreed and in effect.                                |
| Deprecated | No longer relevant, but not replaced.                |
| Superseded | Replaced by a newer ADR (link to it).                |

## Index

| ADR                                                  | Title                           | Status   | Date       |
|------------------------------------------------------|---------------------------------|----------|------------|
| [0001](0001-record-architecture-decisions.md)        | Record architecture decisions   | Accepted | 2026-09-21 |
| [0002](0002-use-csharp-and-dotnet-for-the-backend.md) | Use C# and .NET for the backend | Proposed | 2026-09-21 |
| [0003](0003-use-clean-architecture.md)               | Use Clean Architecture          | Proposed | 2026-09-21 |
| [0004](0004-use-a-relational-database.md)            | Use a relational database       | Proposed | 2026-09-21 |
| [0005](0005-use-postgresql.md)                       | Use PostgreSQL                  | Proposed | 2026-09-21 |
| [0006](0006-use-entity-framework-core-for-data-access.md) | Use Entity Framework Core for data access | Proposed | 2026-09-21 |
| [0007](0007-deploy-with-docker-compose-on-the-homelab.md) | Deploy with Docker Compose on the homelab | Proposed | 2026-09-21 |
