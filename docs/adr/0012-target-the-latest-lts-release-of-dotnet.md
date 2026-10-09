# ADR-0012: Target the latest LTS release of .NET

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0002 chose C# and .NET for the backend, "currently .NET 9, LTS releases
preferred going forward", and left the upgrade policy to be recorded once the
next LTS release shipped. That release, .NET 10, shipped in November 2025.

Microsoft ships a new major version of .NET every November. Even-numbered
versions are Long Term Support (LTS) releases, supported for three years;
odd-numbered versions are Standard Term Support (STS) releases, supported for
two years. As of this ADR:

| Version  | Type | Status                     | End of support |
|----------|------|----------------------------|----------------|
| .NET 8   | LTS  | Maintenance                | 2026-11-10     |
| .NET 9   | STS  | Maintenance                | 2026-11-10     |
| .NET 10  | LTS  | Active (10.0.12)           | 2028-11-14     |
| .NET 11  | STS  | Release candidate          | GA expected November 2026 |
| .NET 12  | LTS  | Not yet released           | GA expected November 2027 |

The service currently targets `net9.0` and builds from the `9.0` SDK and
ASP.NET runtime images, so it falls out of support in about a month.

Forces:

- **Dormancy.** The project is built by one person in their spare time. There
  will be long stretches with no feature work, and the service must stay
  secure and supported through them without a forced framework upgrade.
- **Current features.** The developer wants the newest language and platform
  features available (C# 14, the current ASP.NET Core, EF Core and OpenAPI
  releases), not to sit a version behind for stability's sake.
- **Patch flow.** Security fixes ship monthly as patch releases. Picking them
  up should need no more than a rebuild, not a code change.
- **Lockstep packages.** The `Microsoft.AspNetCore.*` and
  `Microsoft.EntityFrameworkCore.*` packages (ADR-0009) version with the
  runtime, so the framework choice also fixes their major version.
- **Containers.** The service ships as a Linux container to both Azure and the
  homelab (ADR-0006, ADR-0007), so the base images must follow the same
  version as the target framework.

## Decision

We will target the newest LTS release of .NET, which today is .NET 10
(`net10.0`, C# 14), and skip STS releases.

Concretely:

- **Target framework.** Every project targets the same LTS framework, set in
  one place (`Directory.Build.props`) rather than per project.
- **SDK.** A `global.json` at the repository root pins the SDK to the LTS major
  version with `rollForward: latestFeature` and `allowPrerelease: false`, so
  any newer 10.0 SDK is used but an 11.0 SDK or a preview is not picked up by
  accident.
- **Images.** The Dockerfile uses the floating `10.0` tags of the
  `mcr.microsoft.com/dotnet/sdk` and `mcr.microsoft.com/dotnet/aspnet`
  images, so a rebuild picks up the latest patch release.
- **Packages.** Framework-versioned Microsoft packages stay on the same major
  version as the runtime and are kept on the latest patch.
- **Upgrading.** We move to the next LTS release within six months of its GA
  (for .NET 12, by May 2028), which leaves at least six months of overlap
  before the current LTS goes out of support. An upgrade is done in a single
  PR that bumps the target framework, `global.json`, images and lockstep
  packages together.
- **STS releases** are not used on `master`. They may be tried on a branch to
  get ahead of breaking changes in the next LTS.

## Options considered

### Option 1: Target the latest LTS and upgrade once per LTS (chosen)

- Pros: The newest features available on a supported release today; up to
  three years of patches per version, so the service can lie dormant for long
  periods; one planned upgrade every two years; matches the "LTS preferred"
  intent of ADR-0002.
- Cons: Features that land in an STS release arrive up to a year later; each
  upgrade jumps two major versions' worth of breaking changes.

### Option 2: Track every major release, including STS

- Pros: Always on the newest features, as early as they ship.
- Cons: A mandatory upgrade every November regardless of whether the project
  is active, which defeats dormancy; STS releases end support at the same
  time as, or before, the LTS release that precedes them, so they add no
  support runway; more churn in lockstep packages.

### Option 3: Stay on .NET 9 until there is a reason to move

- Pros: No work now.
- Cons: .NET 9 goes out of support on 2026-11-10; it is the oldest supported
  option rather than the newest, so it fails both forces.

### Option 4: Pin the exact SDK and image digests

- Pros: Fully reproducible builds.
- Cons: Every security patch needs a commit, which does not happen while the
  project is dormant; reproducibility of this kind is not a requirement for a
  service of this size.

## Consequences

### Positive

- The service is on a supported runtime until November 2028 and has the
  current C#, ASP.NET Core and EF Core feature set.
- Security patches arrive by rebuilding the image; no code change is needed
  while the project is idle.
- The target framework lives in one file, so the next upgrade touches few
  places.
- Upgrades are infrequent, scheduled and predictable.

### Negative

- Floating image tags and SDK roll-forward mean two builds a month apart may
  use different patch versions. A patch-level regression would surface on
  rebuild rather than on an explicit bump.
- Features introduced in .NET 11 are not available until .NET 12.
- The upgrade deadline must be remembered; nothing in the build fails until
  support has already ended.

### Follow-up

- Retarget the solution to `net10.0`: add `Directory.Build.props` and
  `global.json`, update the Dockerfile images and the
  `Microsoft.AspNetCore.OpenApi` package (in the PR for
  [#4](https://github.com/gyback/facility-management/issues/4)).
- Automate patch updates for NuGet packages and base images so the service
  keeps receiving fixes while dormant
  ([#29](https://github.com/gyback/facility-management/issues/29)).
- Track the upgrade to .NET 12 once it reaches GA (November 2027), with a
  deadline of May 2028
  ([#30](https://github.com/gyback/facility-management/issues/30)).

## References

- Issue [#4: Record the .NET upgrade policy and pin to an LTS release](https://github.com/gyback/facility-management/issues/4)
- [ADR-0002: Use C# and .NET for the backend](0002-use-csharp-and-dotnet-for-the-backend.md)
- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
- [.NET release index](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json)
- [global.json overview](https://learn.microsoft.com/dotnet/core/tools/global-json)
