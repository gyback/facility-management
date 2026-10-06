# ADR-0002: Use C# and .NET for the backend

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

FacilityManagementService is a shared backend API for managing family-shared
facilities (initially two cabins and a private home). It will serve both a web
frontend and a mobile client, and it covers three feature areas: booking and
vacation coordination, instructions and documentation, and facility upkeep
with recurring maintenance, work logs and per-owner cost splitting.

The project is developed by a single person in their spare time, and the build
process is documented publicly as a portfolio piece. The forces at play are:

- Developer productivity matters more than raw throughput; the user base is a
  handful of relatives, not thousands of concurrent users.
- The cost-splitting and recurring-maintenance features involve real domain
  logic that benefits from a statically typed language and a mature ORM.
- The system must ship as a Linux container, and hosting options that scale
  to zero when idle are under consideration, so the runtime must produce
  lean, self-contained images that start quickly from cold.
- Long-term maintainability by one person favours a stable, well-documented
  ecosystem over the newest framework.

## Decision

We will build the backend in C# on .NET (currently .NET 9, LTS releases
preferred going forward) using ASP.NET Core for the HTTP API.

The primary reason is that the sole developer is most proficient in C#, which
directly maximises delivery speed and code quality on a solo project. The .NET
ecosystem also covers every other need of the project well: first-class
Docker support, Entity Framework Core, built-in OpenAPI, OpenTelemetry, and a
long support lifecycle.

## Options considered

### Option 1: C# / .NET with ASP.NET Core

- Pros: Developer's strongest language; mature, batteries-included web
  framework; excellent tooling (Rider, hot reload, analyzers); official
  Docker images; LTS releases with three years of support; strong typing
  suits the domain model.
- Cons: Heavier runtime than Go or Node; smaller open-source hobbyist
  community than JavaScript; some Microsoft-centric conventions.

### Option 2: TypeScript / Node.js (NestJS or similar)

- Pros: Same language as the planned React/Next.js frontend, enabling shared
  types; large ecosystem; lightweight containers.
- Cons: Developer is less proficient; weaker typing at runtime; ORM story
  (Prisma, TypeORM, Drizzle) is less mature than EF Core for complex domain
  models; more churn in the ecosystem.

### Option 3: Go

- Pros: Very small static binaries, fast startup, simple deployment.
- Cons: Developer would be learning the language on the project; minimal
  built-in web framework and ORM support means more hand-written plumbing;
  slower delivery for a solo developer.

## Consequences

### Positive

- Fastest possible ramp-up and highest confidence in the code, since the
  developer is working in their primary language.
- ASP.NET Core provides routing, dependency injection, configuration,
  OpenAPI, authentication and health checks out of the box.
- Multi-project solutions are a first-class concept, so a layered or modular
  structure can be enforced through project references if one is chosen.

### Negative

- Frontend and backend use different languages, so API types must be shared
  via an OpenAPI-generated client rather than a common package.
- Container images are larger than Go or Node equivalents (roughly 100 MB+
  for the ASP.NET runtime base image), and cold start from a scaled-to-zero
  container takes a few seconds. Acceptable for this audience; ReadyToRun
  publishing is available if it becomes a problem.

### Follow-up

- Pin the target framework to an LTS release when the next one ships
  (.NET 10) and record the upgrade policy.
- Generate a TypeScript client from the OpenAPI spec for the frontend.

## References

- [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
