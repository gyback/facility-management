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
5. Open an issue for each item in its Follow-up section that is not already
   tracked (see [Follow-ups](#follow-ups)). If the ADR resolves an open
   `adr-needed` issue, close it from the PR with `Closes #N`.

ADRs are immutable once accepted. To change a decision, write a new ADR and mark
the old one as **Superseded by ADR-XXXX** (linking both ways).

## Ordering and dependencies

ADR numbers are assigned in dependency order: an ADR may build on any ADR with
a lower number and on none with a higher one. This keeps the log readable from
the top down, and it holds even when several ADRs are written in the same
sitting.

- **Context, Decision, Options and Consequences** may rely only on facts,
  requirements, and earlier ADRs. A later decision is never a premise. If a
  later decision is anticipated, state the underlying requirement instead
  (for example "hosting that scales to zero is under consideration, so cold
  start matters", not "we run on Container Apps").
- **Follow-up** is the only section that may point forward, as "this decision
  requires deciding X", with the ADR number once it exists.
- **References** may list later ADRs that build on this one. Adding such a
  link to an accepted ADR is permitted, in the same way as supersession
  links.
- When a batch of ADRs is written together, order it by dependency. A genuine
  cycle between two ADRs means they are one decision, or that the shared
  premise should be stated as a requirement in the earlier one.

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
| [0002](0002-use-csharp-and-dotnet-for-the-backend.md) | Use C# and .NET for the backend | Accepted | 2026-10-06 |
| [0003](0003-use-clean-architecture.md)               | Use Clean Architecture          | Accepted | 2026-10-06 |
| [0004](0004-use-a-relational-database.md)            | Use a relational database       | Accepted | 2026-10-06 |
| [0005](0005-model-bookings-as-non-blocking-stays.md) | Model bookings as non-blocking stays | Accepted | 2026-10-06 |
| [0006](0006-deploy-to-azure-free-tiers-with-homelab-staging.md) | Deploy to Azure free tiers with the homelab as staging | Accepted | 2026-10-06 |
| [0007](0007-keep-the-deployment-target-portable.md) | Keep the deployment portable between Azure and the homelab | Accepted | 2026-10-06 |
| [0008](0008-use-azure-sql-database.md)               | Use Azure SQL Database          | Accepted | 2026-10-06 |
| [0009](0009-use-entity-framework-core-for-data-access.md) | Use Entity Framework Core for data access | Accepted | 2026-10-06 |

## Follow-ups

Follow-ups from accepted ADRs are tracked as GitHub issues rather than in the
ADRs themselves, which stay immutable. Each issue names the ADR it came from.

- [Decisions to record](https://github.com/gyback/facility-management/issues?q=is%3Aissue+is%3Aopen+label%3Aadr-needed)
  (`adr-needed`): decisions that need their own ADR.
- [Implementation tasks](https://github.com/gyback/facility-management/issues?q=is%3Aissue+is%3Aopen+label%3Aadr-follow-up)
  (`adr-follow-up`): work an accepted ADR requires.
