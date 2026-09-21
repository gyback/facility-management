# ADR-0007: Deploy with Docker Compose on the homelab

- **Status:** Proposed
- **Date:** 2026-09-21
- **Deciders:** Gustav Gybäck (sole developer)

## Context

The service will be self-hosted on a homelab: a VM host behind a Ubiquiti
UniFi Cloud Gateway Ultra, on a dedicated VLAN isolated from all other local
devices. The runtime footprint is small: one ASP.NET Core API, one PostgreSQL
instance, and later a frontend and a small number of supporting services
(reverse proxy or tunnel, observability). Users are a handful of relatives
accessing the service over the internet.

Forces:

- Consistency: the same artifact should run identically on the developer
  machine and on the VM.
- Portability: the setup should survive moving to a different VM host, a
  VPS, or a managed container service without rework.
- Operational simplicity: one person maintains this in spare time, so
  orchestration must be minimal and recoverable from a fresh VM with a few
  commands.
- Security: the isolated VLAN means the host should require no inbound port
  forwarding if possible.
- Data safety: losing years of maintenance and expense history is the most
  damaging failure mode, so backups must leave the host.

The API project already includes a multi-stage `Dockerfile` producing a
Linux image from the official `mcr.microsoft.com/dotnet` base images.

## Decision

We will package every component as a Docker image and run the full stack on
the homelab VM with Docker Compose.

Concretely:

- The API is built into an image by the multi-stage `Dockerfile` in the API
  project. Images are built in CI (GitHub Actions) and pushed to a registry
  (GitHub Container Registry); the host pulls images rather than building
  them.
- A single `docker-compose.yml` in the repository defines the API,
  PostgreSQL (with a named volume), and any supporting services. A
  `docker-compose.override.yml` covers local development differences.
- Configuration and secrets are supplied through environment variables and
  an untracked `.env` file on the host, never baked into images.
- Deployment is pull-based from the host (e.g. Watchtower or a scheduled
  `docker compose pull && docker compose up -d`), so nothing on the homelab
  needs to accept inbound connections from CI.

Kubernetes and other orchestrators are explicitly out of scope: the workload
is a single host with a few containers.

## Options considered

### Option 1: Docker images run with Docker Compose on the VM

- Pros: Identical artifact in dev, CI and production; whole stack described
  in one versioned file; recovery on a fresh host is `git clone`, restore
  backup, `docker compose up`; trivially portable to a VPS or to a managed
  container platform later; the existing Dockerfile already supports it.
- Cons: No automatic restart across hosts or zero-downtime rollout;
  updates cause brief downtime (acceptable for this audience); Compose is
  not a scheduler, so anything beyond one host needs a different tool.

### Option 2: Bare-metal / systemd services on the VM

- Pros: No container layer; slightly lower overhead; direct access to logs
  and files.
- Cons: Environment drift between developer machine and VM; manual
  installation of the .NET runtime and PostgreSQL; harder to reproduce or
  move; no isolation between services.

### Option 3: Kubernetes (k3s) on the homelab

- Pros: Declarative, self-healing, industry standard; good portfolio signal.
- Cons: Large operational overhead for one person and one host; ingress,
  storage and secrets management add complexity without benefit at this
  scale; slows delivery of the actual product.

### Option 4: Managed cloud containers (Azure Container Apps, Fly.io) with managed PostgreSQL

- Pros: No hardware or OS to maintain; built-in TLS, scaling and backups.
- Cons: Recurring cost for a hobby project; conflicts with the stated goal of
  running on the existing homelab; data leaves the home network. Remains a
  fallback that the Docker images make easy to adopt.

## Consequences

### Positive

- One build artifact per component, promoted from CI to the host unchanged.
- The full stack is reproducible from the repository plus a database backup.
- Moving to a VPS or a managed container service later requires only a new
  Compose file or manifest, not a rebuild.
- No inbound ports are needed on the UniFi gateway for deployment itself.

### Negative

- Short downtime on each deploy while containers restart.
- The developer is responsible for OS updates, Docker updates and disk
  space on the VM host.
- Secrets live in a `.env` file on the host, which must be protected by file
  permissions and excluded from version control.

### Follow-up

- Add `docker-compose.yml` with `api` and `postgres` services and a named
  volume for PostgreSQL data.
- Add a GitHub Actions workflow that builds and pushes the API image on merge
  to `main`.
- Decide how relatives reach the service (Cloudflare Tunnel, Tailscale, or a
  reverse proxy with a forwarded port) and the matching UniFi firewall
  rules. This warrants its own ADR.
- Set up automated off-host PostgreSQL backups (see ADR-0005 follow-up).
- Decide whether EF Core migrations run on API startup or in a one-shot
  migration container (see ADR-0006 follow-up).

## References

- [ADR-0002: Use C# and .NET for the backend](0002-use-csharp-and-dotnet-for-the-backend.md)
- [ADR-0005: Use PostgreSQL](0005-use-postgresql.md)
- [Docker Compose documentation](https://docs.docker.com/compose/)
- [.NET Docker images](https://learn.microsoft.com/dotnet/core/docker/introduction)
