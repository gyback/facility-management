# ADR-0006: Deploy to Azure free tiers with the homelab as staging

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

The runtime footprint is small: one ASP.NET Core API, one relational
database, a web frontend, and a few supporting concerns (secrets, telemetry,
identity). Users are a handful of relatives reaching the service over the
internet, mostly in short bursts around holidays and weekends, with long idle
periods in between.

Two deployment targets are available:

- A homelab: a VM host behind a Ubiquiti UniFi Cloud Gateway Ultra on an
  isolated VLAN, running Docker. Zero recurring cost, full control, family
  data stays at home, but exposing it to relatives needs a tunnel or VPN and
  all operations (patching, backups, uptime) fall on one person.
- Azure: managed services with permanent free grants that cover this
  workload, managed TLS and backups, no inbound exposure of the home
  network, and a strong portfolio signal. Costs money if usage exceeds the
  grants, and the free database auto-pauses when idle.

The project is a public portfolio piece, so demonstrated cloud practice has
value beyond the running service. The API project already contains a
multi-stage `Dockerfile` producing a Linux image.

## Decision

We will run production in Azure on permanently free tiers, and use the
homelab as a staging environment running the same container images with
Docker Compose.

Production:

- **API** on Azure Container Apps, consumption plan, scaled to zero when
  idle, sized at the smallest CPU and memory combination.
- **Database** on a managed Azure database service with a permanent free
  tier. The engine is a separate decision; this ADR only requires that a
  free managed option exists and that the same engine can run in a container
  on the homelab.
- **Frontend** on Azure Static Web Apps free tier as a static or single-page
  build, so no second server-rendering container is needed.
- **Secrets** in Azure Key Vault, referenced from Container Apps; the API
  authenticates to the database with a managed identity.
- **Telemetry** via OpenTelemetry to Application Insights within the Log
  Analytics free allowance.
- **Images** built by GitHub Actions on merge to `main` and pushed to GitHub
  Container Registry. GitHub Actions authenticates to Azure with OIDC
  federated credentials; no cloud secrets are stored in GitHub.
- **Infrastructure** defined as code; the tool (Bicep or Terraform) is
  chosen in a follow-up ADR.

Staging on the homelab:

- A `docker-compose.yml` runs the same API image tag that is heading to
  production, plus a containerised instance of the chosen database engine
  and the frontend.
- Exposed only on the home network (or over a VPN for the developer), so the
  tunnel-versus-port-forward question does not arise for staging.
- Also serves as the fallback production environment if Azure is ever
  abandoned. This requires that the application stays portable between the
  two environments, which is a decision in its own right (see Follow-up).

Quota discipline, so that production stays free:

- Health and readiness probes never touch the database.
- No polling background jobs; scheduled work runs at most daily.
- No warm-up pings to hide cold starts.
- Budget alert on the subscription at 5 USD per month from day one.

## Options considered

### Option 1: Azure free tiers for production, homelab as staging

- Pros: Near-zero recurring cost; managed TLS, certificates, backups and
  identity; no inbound ports on the home network; every image is exercised in
  staging first; the homelab remains a ready fallback; visible cloud
  practice for reviewers.
- Cons: Cold starts after idle on both the API (seconds) and the database
  (up to a minute); free grants can change; usage discipline required; a
  second environment to keep in sync.

### Option 2: Homelab for production with Docker Compose

- Pros: Zero cost forever; always on; data never leaves the home; simplest
  possible stack.
- Cons: Must expose the home network via tunnel, VPN or port forward;
  backups, patching and uptime are manual; less portfolio signal; a power or
  ISP outage takes the service down.

### Option 3: Azure with paid tiers (Container Apps always-on, managed PostgreSQL)

- Pros: No cold starts; open source database.
- Cons: Roughly 20 to 40 USD per month with no upper benefit for a family
  workload; deferred rather than rejected, and reachable by changing a tier.

### Option 4: Kubernetes on the homelab

- Pros: Industry standard; self-healing.
- Cons: Far more operational overhead than a single-host workload
  justifies; slows delivery of the actual product.

## Consequences

### Positive

- Production costs nothing at expected usage and needs no inbound exposure
  of the homelab.
- Staging on the homelab gives a realistic rehearsal of every deploy,
  including migrations, on identical images.
- CI-to-cloud with OIDC, managed identity, Key Vault and infrastructure as
  code are all present and reviewable.
- The Compose stack that runs staging is also the fallback production
  environment, so the door back to the homelab stays open.

### Negative

- Cold starts are the visible cost. The frontend must show a clear loading
  state, and the family must be told once that the first open of the day is
  slow.
- Free grants and offers are Microsoft's to change; the budget alert and the
  rehearsed homelab fallback are the mitigation.
- Two environments to configure and keep in sync; configuration must be
  strictly environment-driven.
- Family data lives in an Azure region rather than at home. Sweden Central
  keeps it in the EU.

### Follow-up

- Choose the infrastructure-as-code tool in a new ADR and provision the
  resource group, Container App, database, Key Vault and Static Web App
  from it.
- Add a GitHub Actions workflow: build and push the image, run tests, deploy
  to staging, then deploy to production on approval.
- Define the portability constraints that keep the homelab viable as a
  fallback as a new ADR.
- Choose the database engine, which must have a free managed Azure tier and
  a container image for staging as a new ADR.
- Add `docker-compose.yml` for staging with `api`, database and `web`
  services.
- Decide on the migration step in the pipeline (a Container Apps job before
  the new revision).
- Document the cold-start behaviour for the family in the app's help page.

## References

- [ADR-0002: Use C# and .NET for the backend](0002-use-csharp-and-dotnet-for-the-backend.md)
- [Azure Container Apps pricing and free grant](https://azure.microsoft.com/pricing/details/container-apps/)
- [Azure Static Web Apps](https://learn.microsoft.com/azure/static-web-apps/)
- [GitHub Actions OIDC with Azure](https://learn.microsoft.com/azure/developer/github/connect-from-azure)
