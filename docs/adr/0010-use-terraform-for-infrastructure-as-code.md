# ADR-0010: Use Terraform for infrastructure as code

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0006 runs production on Azure free tiers and requires the infrastructure
to be defined as code, leaving the choice of tool to this ADR. The resources
to provision are a resource group, a Container Apps environment and app, the
Azure SQL Database free offer (ADR-0008), a Key Vault, a Static Web App,
Application Insights with its Log Analytics workspace, and a budget alert on
the subscription. GitHub Actions deploys to Azure with OIDC federated
credentials, so an Entra ID application or managed identity with federated
credentials also has to exist, and ideally be defined in code as well.

The homelab staging environment is a Docker Compose stack (ADR-0006,
ADR-0007). It is configured by the Compose file, not by the infrastructure
tool, so the tool only has to target Azure.

Forces:

- One developer maintains everything, so the tool should be one they can
  work in fluently. The developer has prior Terraform experience and none
  with Bicep.
- Changes should be reviewable before they are applied: a pull request
  should show what will be created, changed or destroyed.
- No cloud secrets are stored in GitHub (ADR-0006); the tool must
  authenticate from CI with OIDC.
- Recurring cost must stay within the 5 USD budget, ideally at zero.
- The project is a public portfolio piece, so a widely recognised tool and
  idiomatic use of it have value beyond this repository.
- Some resources are configured through recent or preview Azure settings,
  such as the free-offer flags on Azure SQL Database, so the tool needs a
  way to reach any Azure API version.

## Decision

We will use Terraform to define and provision all Azure infrastructure.

- **Layout.** Configuration lives in `infra/` at the repository root. There
  is a single production environment; staging is not managed by Terraform.
  Modules are introduced only when a pattern repeats, not up front.
- **Providers.** `azurerm` for Azure resources, `azuread` for the Entra ID
  application and federated credentials used by GitHub Actions, and
  `azapi` only where `azurerm` does not yet expose a setting. The Terraform
  version and every provider are pinned, and the dependency lock file is
  committed.
- **State.** Remote state in a blob container in an Azure Storage account,
  with blob versioning enabled, shared-key access disabled, and access
  through Entra ID authentication only. The state storage account and the
  CI identity are created once by a small bootstrap configuration in
  `infra/bootstrap/` with local state, which is the only manual
  `terraform apply`.
- **Pipeline.** GitHub Actions authenticates to Azure with OIDC for both the
  providers and the state backend. Every pull request touching `infra/` runs
  `terraform fmt -check`, `terraform validate` and `terraform plan`, and
  posts the plan to the pull request. `terraform apply` runs on merge to
  `master` behind the same manual approval as the production deployment.
- **Boundaries.** Terraform owns infrastructure only. Container image
  revisions are rolled out by the deployment workflow and database schema by
  the migration step (ADR-0009); Terraform ignores changes to the image tag
  of the Container App so the two do not fight.
- **Secrets.** Terraform creates the Key Vault and grants access to it, but
  no secret value is written in Terraform code or variables files. Where a
  value is unavoidable it is generated in Terraform, which keeps it out of
  the repository but not out of the state, so state access is treated as
  secret access.

## Options considered

### Option 1: Terraform

- Pros: The developer already knows it, so infrastructure work starts
  immediately; `terraform plan` gives a reliable, reviewable diff for every
  pull request; one tool covers Azure, Entra ID and, if wanted later,
  GitHub repository settings; the most widely used infrastructure-as-code
  tool, so the skill and the code are recognisable to any reviewer; mature
  OIDC support in both the providers and the `azurerm` backend; `azapi`
  reaches any Azure API version when `azurerm` lags.
- Cons: Requires a state file and a place to keep it, plus a bootstrap step
  for that place; the state holds generated secrets in plain text;
  `azurerm` sometimes trails new Azure features by weeks; licensed under the
  Business Source License since 2023, which does not restrict this project
  but is no longer open source.

### Option 2: Bicep

- Pros: First-party Azure language with day-zero support for new resource
  types and API versions; no state file, since Azure Resource Manager is the
  state; no extra storage account or bootstrap; free and open source.
- Cons: New to the developer, so the first deployment takes longer;
  `what-if` previews are noisier and less trustworthy than a Terraform plan;
  Azure only, so the skill does not transfer to other clouds; Entra ID
  objects need the separate Microsoft Graph extension; less portfolio signal
  outside Azure-focused teams.

### Option 3: OpenTofu

- Pros: Open-source fork of Terraform under the Linux Foundation; the same
  language, providers and workflow, so the developer's experience carries
  over; adds client-side state encryption.
- Cons: Smaller community and less recognition among reviewers than
  Terraform; the licensing difference does not matter for this project.
  Kept as an exit route rather than chosen: the configuration stays within
  the language subset both tools share, so switching is a binary swap.

### Option 4: Pulumi with C#

- Pros: Infrastructure in the same language as the backend (ADR-0002);
  real loops, types and tests.
- Cons: New to the developer; state needs either Pulumi Cloud or a
  self-managed backend; general-purpose code invites cleverness in what
  should be a declarative description; smaller audience than Terraform.

### Option 5: Azure CLI scripts or portal configuration

- Pros: Nothing to learn or host.
- Cons: Not declarative, so drift is invisible and changes are not
  reviewable; contradicts ADR-0006.

## Consequences

### Positive

- Infrastructure work starts in a familiar tool, so effort goes into the
  Azure design rather than into learning a language.
- Every infrastructure change is visible as a plan in its pull request
  before it is applied.
- The CI identity and its federated credentials are themselves in code, so
  the whole Azure footprint can be recreated from the repository plus the
  bootstrap step.
- The configuration can move to OpenTofu without a rewrite if the licence
  or the project's direction ever makes that preferable.

### Negative

- A storage account for state is one more resource to secure and pay for.
  At a few kilobytes of state the cost is a few cents per month, within the
  budget but not strictly zero.
- The bootstrap configuration is a chicken-and-egg step run by hand with
  owner rights, and must be documented so it can be repeated.
- Plain-text secrets in state mean read access to the state container is as
  sensitive as read access to Key Vault.
- When `azurerm` lags behind Azure, the `azapi` fallback is more verbose and
  less type-checked than native resources.
- Image tag drift between Terraform and the deployment workflow has to be
  handled deliberately with `ignore_changes`, or the next `apply` would roll
  back a deployment.

### Follow-up

- Create the bootstrap configuration for the state storage account and the
  GitHub Actions identity, and document how to run it (tracked as FM-23).
- Provision the resource group, Container Apps environment and app, Azure
  SQL Database free offer, Key Vault, Static Web App, Application Insights
  and Log Analytics workspace from `infra/` (tracked as FM-13).
- Define the 5 USD monthly budget alert in Terraform (tracked as FM-14).
- Add `plan` on pull requests and approved `apply` on merge to the GitHub
  Actions workflow (tracked as FM-16).

## References

- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [Issue #1: Choose the infrastructure-as-code tool](https://github.com/gyback/facility-management/issues/1)
- [Terraform AzureRM backend](https://developer.hashicorp.com/terraform/language/backend/azurerm)
- [AzAPI provider](https://registry.terraform.io/providers/Azure/azapi/latest/docs)
- [Bicep documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [OpenTofu](https://opentofu.org/)
