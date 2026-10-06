# ADR-0007: Keep the deployment portable between Azure and the homelab

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0006 runs production on Azure free tiers and staging on the homelab with
Docker Compose. The two environments must run the same images and the same
schema. Beyond that, the homelab is the fallback production environment if
Azure's free grants change or the cloud path is abandoned, and a move back is
itself a useful, documentable exercise for this portfolio project.

The risk in "keeping the door open" is speculative abstraction: interfaces
with a single implementation, written for a platform we may never return to.
The constraints below are limited to practices that are worth following for
staging parity alone, so they cost nothing extra.

## Decision

We will keep the system portable between Azure and the homelab by holding to
the following constraints from the first deployment onward. Any change that
violates one of them requires a new ADR.

1. **Configuration only through the environment.** All settings and secrets
   reach the API via environment variables or mounted files read through
   `IConfiguration`. Key Vault references on Azure and an `.env` file on the
   homelab both resolve to the same configuration keys. Nothing is baked into
   images and no host-specific file is required.
2. **Stateless API containers.** The API keeps no state on its local
   filesystem or in process memory that must survive a restart. Anything that
   must persist lives in the database or in a store introduced with its own
   ADR when actually needed.
3. **Migrations are a separate, explicit step.** Schema migrations run from
   a dedicated entry point independent of starting the API, so that Azure can
   run them as a pre-deploy job and Compose can run them as a one-shot
   service.
4. **Engine-neutral schema.** Migrations contain no engine-specific SQL.
   Invariants are unique indexes, foreign keys and check constraints.
   Semi-structured content is mapped through the data access layer rather
   than through engine-specific column types. This keeps the schema movable
   between the managed engine in Azure, its containerised equivalent on the
   homelab, and a different engine if one is ever needed.
5. **Database authentication is pluggable.** The connection uses managed
   identity on Azure and a password on the homelab, selected by
   configuration, with no code path that assumes either.
6. **Images are built in CI and pulled from a registry.** Neither environment
   builds images. The same image digest that ran in staging is what runs in
   production.
7. **Telemetry goes through OpenTelemetry.** The exporter is chosen by
   configuration, so Application Insights on Azure and a self-hosted
   collector on the homelab use identical instrumentation.
8. **Standard container contract.** The API listens on the port given by
   `ASPNETCORE_HTTP_PORTS`, exposes `/health/live` and `/health/ready`
   without touching the database, trusts forwarded headers from the ingress,
   and handles `SIGTERM` with graceful shutdown.

If production ever moves back to the homelab, it will be recorded as a new
ADR superseding ADR-0006, with a migration runbook and a short retrospective
of what these constraints failed to anticipate.

## Options considered

### Option 1: Constrain both environments so either can be production

- Pros: The constraints are required for staging parity anyway; a move in
  either direction is scoped to infrastructure and CI work; the fallback is
  always rehearsed because staging runs on it daily.
- Cons: Some conveniences are off the table, such as provider-specific SQL
  or building images on the host; discipline is needed to keep the list
  honest.

### Option 2: Optimise for Azure only

- Pros: Free to use every Azure-specific feature.
- Cons: Staging on the homelab drifts from production; a return to the
  homelab becomes a rewrite; free-tier changes become a forced migration
  under pressure.

### Option 3: No staging, homelab as a development box only

- Pros: One environment to maintain.
- Cons: Migrations and images are first exercised in production; no
  rehearsed fallback.

## Consequences

### Positive

- The deployment target is a swappable detail; the application does not
  know where it runs.
- Staging parity and portability are the same work, done once.
- The constraints double as a code review checklist.

### Negative

- Provider-specific features are unavailable even when convenient.
- A single unnoticed violation can quietly re-couple the system to one
  environment, so the checklist must actually be applied in review.

### Follow-up

- Add a "portability" item to the pull request template pointing at this
  ADR.
- Choose the OpenTelemetry backend for the homelab in an observability ADR.

## References

- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
