# ADR-0008: Use Azure SQL Database

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0004 selects a relational database. ADR-0006 deploys production to Azure
on free tiers, with the homelab as a staging environment. The database
engine must therefore have a genuinely free managed offering in Azure and a
free, containerised equivalent for staging and local development.

The booking model in ADR-0005 doesn't need continuous range exclusion.
The remaining hard guarantees (exclusive nights per facility, and later room
claims per night) are plain unique indexes, and the expense ledger relies on
ordinary transactions and foreign keys. No engine-specific feature is
required by the domain.

Forces:

- Recurring cost should be zero or near zero for a family application that
  is idle most of the day.
- Backups and point-in-time restore should be managed, since losing years of
  maintenance and expense history is the worst failure mode.
- First-class support from the .NET data access ecosystem and familiarity
  for a C# developer reduce friction.
- The schema should stay engine-neutral enough that moving back to a
  self-hosted database on the homelab is cheap (ADR-0007).
- Demonstrating Azure competence is a stated goal of this portfolio project.

## Decision

We will use Azure SQL Database under the Azure SQL Database free offer
(General Purpose, serverless, auto-pause) as the production system of record,
and SQL Server Express in a Docker container for staging on the homelab and
for local development.

The free offer provides 100,000 vCore-seconds of compute and 32 GB of storage
per month with no expiry, which comfortably covers a family application that
is idle most of the day. When the monthly quota is exhausted the database is
configured to continue at normal billing rather than pause until the next
month, with a budget alert as the safeguard, so the family never faces an
outage that nobody can fix.

We will keep the schema engine-neutral: no provider-specific SQL in
migrations, JSON stored through the data access layer's provider-independent
mapping, money as `decimal`, and all hard invariants expressed as unique
indexes, foreign keys and check constraints.

## Options considered

### Option 1: Azure SQL Database, free offer

- Pros: Free with no expiry; managed backups with seven-day point-in-time
  restore; first-party .NET drivers and ORM providers; no naming-convention
  friction with .NET; SQL Server Express runs the same schema in Docker for staging; strong
  Azure signal (serverless, managed identity, Entra authentication).
- Cons: Auto-pause means the first request after idle waits tens of seconds
  for the database to resume; the quota is easy to burn with polling or
  database-backed health checks; SQL Server is proprietary, so the
  self-hosted return path is Express with a 10 GB limit; no range or
  exclusion constraints, which the domain no longer needs.

### Option 2: Azure Database for PostgreSQL Flexible Server, Burstable B1ms

- Pros: Open source engine; richest constraint and JSON support; Npgsql
  provider is excellent; free for twelve months on a new Azure account.
- Cons: Roughly 15 to 20 USD per month after the first year with no
  permanent free tier; no auto-pause, so the cost is fixed regardless of
  use; the engine features it offers over SQL Server are no longer required.

### Option 3: PostgreSQL in a container on a small Azure VM

- Pros: Cheapest way to keep PostgreSQL, roughly 5 to 10 USD per month for
  the whole stack; identical to the homelab Compose setup.
- Cons: No managed backups or patching; demonstrates nothing about Azure
  beyond creating a VM; still a recurring cost.

### Option 4: Azure Cosmos DB free tier

- Pros: Free with no expiry.
- Cons: Document model; contradicts ADR-0004.

## Consequences

### Positive

- Zero recurring database cost at the expected usage level, indefinitely.
- Backups, restore, patching and encryption at rest are managed.
- Staging and local development use the same engine family via a free
  container image, so migrations are exercised before they reach production.
- The engine-neutral schema keeps both PostgreSQL and SQL Server Express
  viable as a self-hosted fallback.

### Negative

- Cold resume after auto-pause is visible to users. The first person to open
  the app after a quiet day may wait up to a minute. This is accepted for a
  family audience and documented in the frontend as a loading state.
- Usage discipline is required: no database-backed health probes, no
  polling background jobs, and a budget alert from day one (see ADR-0006).
- Local development needs a SQL Server container, which is heavier than
  PostgreSQL (about 2 GB of memory).
- Moving off Azure later means SQL Server Express or a PostgreSQL migration
  of the schema. The engine-neutral rule keeps the latter mechanical.

### Follow-up

- Provision the free-offer database with auto-pause at the minimum delay and
  the quota-exhausted behaviour set to continue billing.
- Create a budget alert on the subscription at 5 USD per month.
- Add the SQL Server Express service to the Docker Compose stack for staging
  and local development.
- Connect from Azure Container Apps using managed identity and Entra
  authentication rather than a SQL password.

## References

- [ADR-0004: Use a relational database](0004-use-a-relational-database.md)
- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [Azure SQL Database free offer](https://learn.microsoft.com/azure/azure-sql/database/free-offer)
- [SQL Server Express container image](https://hub.docker.com/r/microsoft/mssql-server)
