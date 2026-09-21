# ADR-0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-09-21
- **Deciders:** Gustav Gybäck (sole developer)

## Context

We need a lightweight way to capture significant architectural decisions for
FacilityManagementService — along with their context and trade-offs — so that
current and future team members can understand why the system looks the way it
does without relying on memory or scattered chat threads.

## Decision

We will use Architecture Decision Records (ADRs), as described by Michael Nygard
in [Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

ADRs are stored as Markdown files in `docs/adr/`, versioned alongside the code,
and follow the structure in [`template.md`](template.md).

## Options considered

### Option 1: ADRs in the repository

- Pros: Versioned with the code, reviewed in PRs, easy to find.
- Cons: Less visible to non-developers.

### Option 2: Wiki / Confluence pages

- Pros: Accessible to the wider organisation.
- Cons: Drifts from the code, not part of the review process.

## Consequences

### Positive

- Decisions and their rationale are discoverable and reviewable.
- New team members can onboard by reading the ADR log.

### Negative

- Small overhead of writing an ADR for each significant decision.

### Follow-up

- Write ADRs for existing decisions worth capturing (e.g. .NET / ASP.NET Core,
  Docker-based deployment, Architectural pattern chosen).

## References

- [adr.github.io](https://adr.github.io/)
