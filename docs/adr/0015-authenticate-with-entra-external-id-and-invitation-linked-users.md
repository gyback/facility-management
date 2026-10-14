# ADR-0015: Authenticate with Entra External ID and link identities to users by invitation

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** Gustav Gybäck (sole developer)

## Context

No ADR covers identity yet, but every feature needs it. A stay has an owner
(ADR-0005), a comment has an author (ADR-0013), and members hold roles per
facility (ADR-0004). ADR-0003 names identity integration and "current user"
as infrastructure concerns, with the interface in Application and the
implementation outside it.

Forces:

- **Users.** A handful of relatives, most of them not technical, signing in
  from phones and laptops a few times a year. Forgotten passwords are the
  most likely support request, and the support desk is one person.
- **Closed membership.** The service is private to the family. Being able
  to sign in must not, on its own, give access to anything. Someone already
  in the family has to let a new person in.
- **Cost.** Production must stay inside permanent free grants (ADR-0006).
  Sending email or SMS ourselves would add a paid or self-run service.
- **Portability.** ADR-0007 requires that the application does not know
  where it runs. Settings come only from configuration, API containers are
  stateless, and no code path assumes either Azure or the homelab. The API
  and the frontend are hosted on different origins (ADR-0006).
- **Layering.** Application depends only on Domain, plus the read-side EF
  Core exception in ADR-0014. It cannot see `HttpContext` or
  `ClaimsPrincipal`. Commands and queries are separate, and queries never
  write (ADR-0011).
- **Authorisation follows ownership.** ADR-0013 already puts the access
  check for a comment thread in the owning feature's handler. Whether
  someone may act on a stay, an expense or a member list depends on their
  role in that facility. The identity provider knows nothing about that.
- **Local development and tests.** Running the API, the staging stack and
  the integration tests should not need an internet connection or a real
  account.

## Decision

We will use **Microsoft Entra External ID** as the identity provider through
standard OpenID Connect. Identities are linked to our own users only through
invitations, and a small `ICurrentUser` abstraction gives handlers the
caller's id. Authorisation is checked in handlers and domain code against
data in our own database.

### Sign-in

- Production uses an Entra External ID external tenant on the free core
  tier. Relatives sign in with **email and a one-time passcode**, so there
  are no passwords to forget. Social providers can be added later in the
  tenant without code changes.
- The SPA signs in with the authorisation code flow with PKCE and calls the
  API with a bearer access token. It uses a generic OpenID Connect client,
  not a provider-specific SDK, so the same build works against any
  compliant provider.
- The API validates tokens with the standard JWT bearer handler. Authority,
  audience, the name of the subject claim and a short provider key (for
  example `entra`) come from configuration. The subject claim is `oid` for
  Entra, which is stable per user across app registrations, and `sub` for
  other providers. Inbound claim mapping is turned off so claim names are
  the ones in the token.
- A fallback authorisation policy requires an authenticated caller whose
  identity is linked to a user on every endpoint. Only `AcceptInvitation`
  and reading one's own sign-in state opt out to a policy that requires
  authentication alone. The health probes and the OpenAPI document are
  anonymous.
- The API keeps no session and issues no cookies.

### Identity to user

- A `User` aggregate (a new `Users/` feature) holds our own `UserId`, a
  display name, and the external identities linked to it. Each identity is
  a provider key and a subject, with a unique index on the pair. The
  provider key is the configured name, not the token's `iss` claim. Entra's
  issuer string differs between v1 and v2 access tokens and depends on
  tenant settings, and a change there must not unlink every user. Stays,
  comments and memberships reference `UserId`, never a provider subject, so
  changing provider means re-linking identities rather than rewriting data.
- Users exist only through **invitations**. A facility administrator invites
  someone to a facility with a role. The API returns a single-use link with
  a random token, stored hashed and valid for 14 days. The administrator
  shares the link however they like, so the system sends no email.
- Opening the link signs the person in and sends the
  `AcceptInvitation` command. If their identity is not linked yet, the
  command creates a `User` (display name taken from the token, editable
  later) and links the identity. Either way, it adds the facility
  membership and spends the invitation. This is the only way an identity
  becomes linked. Email addresses are never used for matching, so the API
  does not need to trust an `email_verified` claim.
- An identity that signs in without being linked gets `403` with a
  dedicated problem type from the fallback policy, before any handler runs.
  The SPA shows "ask a family member for an invitation". Only
  `AcceptInvitation` and reading one's own sign-in state are allowed for
  unlinked identities. This check does not depend on any facility, so it
  sits in the API rather than relying on each handler.
- The first user cannot be invited by anyone. A one-off command in the
  migration entry point creates the first facility and prints an
  administrator invitation link. The user who accepts it is also allowed
  to create facilities (see [Creating facilities](#creating-facilities)).

### `ICurrentUser`

The Application project defines:

```csharp
public interface ICurrentUser
{
    /// The user making the request. Throws UnregisteredUserException if the
    /// signed-in identity is not linked to a user.
    UserId Id { get; }

    /// The provider identity behind the request. Used only to link it.
    ExternalIdentity Identity { get; }
}

public sealed record ExternalIdentity(string Provider, string Subject, string? DisplayName);
```

- The implementation is scoped. After authentication and before
  authorisation, a middleware in the API reads the configured subject claim
  and pairs it with the configured provider key. Infrastructure looks up
  the linked `UserId` with one indexed query, and the result is stored in
  the scoped instance, where the fallback policy also reads it. `Id` is
  therefore a plain property, and resolving it never writes. Its exception
  is a backstop for code that runs outside the policy.
- Handlers pass `UserId` into domain methods as an argument, for example
  `stay.Reschedule(by: userId, ...)`. Domain code never sees
  `ICurrentUser`.
- `ICurrentUser` has no roles or claims beyond identity. Roles are per
  facility, and the one permission outside any facility is on the `User`
  aggregate. Both live in our database.

### Per-facility authorisation

- **Everything is scoped to what the caller can see.** A caller sees a
  facility, and everything in it, only if they are a member of it.
  Anything outside that scope is not found (`404`), exactly as if it did
  not exist, so a response never reveals that it exists. Inside the scope,
  a caller whose role or ownership does not allow a change gets `403`.
- **What a role allows** is domain logic. The `Facility` aggregate answers
  "what is this user's role here" and exposes rule checks such as "may
  manage members". Ownership rules, such as only the owner may change a
  stay, live in the aggregate that owns the data and take the acting
  `UserId`.
- **Command handlers check before they act**, in two steps. They load the
  aggregates through their repositories (ADR-0014) and first check that
  the caller is a member of the facility the target belongs to, failing
  with the not-found error if not. Only then do they call the domain rule
  check and throw `ForbiddenException` if it fails. Every command that
  touches facility data does this explicitly. There is no decorator or
  endpoint attribute for it.
- **Query handlers scope the projection** to facilities where the caller is
  a member. Data in other facilities is simply not found.
- The API layer enforces only that the caller is authenticated and linked
  to a user. It has no per-facility roles or policies.
- **There is no system-wide role.** Every permission inside a facility
  comes from a facility role, and nothing grants access across facilities.
  Operator tasks such as bootstrapping or data repair run outside the API,
  through the migration entry point or directly against the database.
  Adding a system-wide role needs a new ADR, because it would bypass the
  facility scoping in every query handler.

### Creating facilities

Creating a facility happens before any facility role exists, so it is the
one permission that cannot come from a facility.

- The `User` aggregate has a single capability, `CanCreateFacilities`. It
  allows creating a facility and nothing else. The creator becomes the new
  facility's administrator, after which the per-facility rules apply. It
  gives no access to other facilities.
- The create-facility handler loads the caller's `User` and the domain
  refuses with `ForbiddenException` if the capability is missing, the same
  way as any other check.
- The capability is granted and revoked only through a command in the
  migration entry point, and to the first user by the bootstrap. The API
  has no endpoint for it, so no one can grant it to themselves through the
  application. Revoking it does not affect facilities already created.

### Operator commands

ADR-0007 and ADR-0009 introduced the migration entry point to apply schema
migrations. This decision widens it to a small set of operator commands:
bootstrapping the first user, and granting and revoking
`CanCreateFacilities`. Each is a separate, explicitly named subcommand. The
pre-deploy job and the Compose one-shot service run only the migration
subcommand, so a deployment never runs an operator command.

### Local development, tests and staging

- Local development and the homelab staging stack run a
  **mock OpenID Connect server** as a container in Docker Compose (for
  example `navikt/mock-oauth2-server`, MIT-licensed). It issues real signed
  tokens with any subject entered on its login page. The API uses the same
  JWT bearer handler against it, with a different authority in
  configuration. There is no development-only authentication scheme and no
  code path that differs from production.
- Integration tests mint tokens in-process with a test signing key that
  replaces the handler's configured keys, so they need no network.
- Production, and the homelab if it ever becomes production again
  (ADR-0007), point at Entra External ID. Entra is an external service, so
  it is reachable from either host.

## Options considered

### Identity provider

#### Option 1: Entra External ID with standard OIDC (chosen)

- Pros: Free for the first 50,000 monthly active users; passwordless email
  sign-in for relatives; Microsoft runs passcode delivery, account recovery
  and brute-force protection; reachable from Azure and the homelab alike;
  used only through standard protocols, so it is replaceable; fits the
  Azure story of the project.
- Cons: A separate external tenant to set up and keep; provider-specific
  quirks (the `oid` claim, scope naming) are first exercised in production
  unless tested separately; Microsoft's terms and grants can change.

#### Option 2: Azure AD B2C

- Pros: Mature, with many samples.
- Cons: Closed to new customers since May 2025, with External ID as its
  successor.

#### Option 3: ASP.NET Core Identity with our own accounts

- Pros: No external service; fully portable; complete control over data.
- Cons: We store credentials and own password reset, lockout and MFA;
  passwordless or reset flows need an email service, which is not free to
  run; cookie sessions need a Data Protection key ring shared across
  container restarts, which is state ADR-0007 keeps out of the API.

#### Option 4: Self-hosted OIDC server (Keycloak, Authentik, Zitadel)

- Pros: Open source; runs on the homelab; standard protocols.
- Cons: Another stateful service with its own database to run in Azure,
  where it would not fit the free grants or scale to zero; patching an
  internet-facing identity server falls on one person.

#### Option 5: Another hosted provider (Auth0, Clerk) on a free tier

- Pros: Polished developer experience; free tiers large enough.
- Cons: Works equally well through standard OIDC, but adds a third vendor
  whose free-tier terms we do not control, with no advantage over the
  provider already in the platform we deploy to.

#### Option 6: Container Apps built-in authentication

- Pros: Almost no code.
- Cons: Exists only on Azure, so the homelab would need a different
  mechanism, which violates ADR-0007; tokens are handled outside the
  application and are hard to test.

### Session handling in the browser

#### Option 1: Bearer tokens obtained by the SPA (chosen)

- Pros: The API stays stateless; works across the two origins; one
  validation path for every environment.
- Cons: Tokens live in the browser, so an XSS flaw could steal them. Short
  token lifetimes and a strict Content Security Policy are the mitigation.

#### Option 2: Backend-for-frontend with cookies

- Pros: Tokens never reach the browser; the approach current browser-app
  guidance prefers.
- Cons: Cross-site cookies between the frontend and API origins are
  blocked by browsers unless both share a custom domain; it needs server
  sessions and a persisted Data Protection key ring, which ADR-0007 keeps
  out of the API.

### Linking identities to users

#### Option 1: Invitation links (chosen)

- Pros: Membership stays closed; no email is sent or trusted; one mechanism
  both creates users and adds existing users to more facilities; works with
  any provider.
- Cons: The administrator must pass the link on; a leaked unused link lets
  someone else in until it expires or is used.

#### Option 2: Match on a verified email address

- Pros: No link to share; an administrator types an address.
- Cons: Depends on each provider asserting verified email correctly;
  relatives with several addresses end up unmatched; an address change at
  the provider breaks nothing but confuses support.

#### Option 3: Open self-registration

- Pros: Least friction.
- Cons: Anyone with an account at the provider becomes a user; contradicts
  closed membership.

### Where per-facility authorisation lives

#### Option 1: Domain rules, checked explicitly in handlers (chosen)

- Pros: Rules are unit-tested with the aggregates; the check sits next to
  the data it depends on, so a command never needs an extra lookup; follows
  ADR-0013, where the owning feature checks access; a reader sees the check
  in the handler.
- Cons: Every handler must remember to check, so tests per handler for the
  non-member and wrong-role cases are the safety net.

#### Option 2: ASP.NET Core resource-based policies at the endpoints

- Pros: Idiomatic ASP.NET Core; checks are visible in endpoint metadata.
- Cons: The API layer would need to load facility data, duplicating the
  handlers' loading; often the facility is only known after loading the
  stay or expense; rules sit outside Domain and are bypassed by anything
  that calls a handler directly.

#### Option 3: An authorisation decorator driven by a marker interface

- Pros: Applied automatically, like validation and transactions
  (ADR-0011).
- Cons: Needs the facility id on every message, which messages addressed by
  a stay or comment id do not have; only covers membership, not roles or
  ownership, so handlers still check; hides an important rule behind
  convention.

#### Option 4: Add a system-wide administrator role

- Pros: Support and data repair possible from the application, without
  database access.
- Cons: A bypass in every domain check and every query's facility scoping;
  one powerful account to protect; no current need, since operator tasks
  already have a route outside the API.

### Who may create facilities

#### Option 1: A `CanCreateFacilities` capability granted out of band (chosen)

- Pros: The right is held by named, trusted people; one flag, checked in
  Domain like every other rule; cannot be self-granted through the API;
  can be handed to a trusted relative without making them an operator.
- Cons: Granting it needs operator access to run the entry point command.

#### Option 2: An allow-list of identities in configuration

- Pros: No schema change.
- Cons: Holds provider subjects or `UserId`s in configuration, tying it to
  the provider or to database ids; every change needs a redeploy.

#### Option 3: Any facility administrator may create facilities

- Pros: No new concept.
- Cons: The right spreads to everyone made an administrator anywhere,
  which is what this decision is meant to prevent.

#### Option 4: Roles issued by the provider (Entra app roles)

- Pros: Managed in the provider's portal; arrive in the token.
- Cons: Provider-specific, so the mock server and any future provider must
  imitate them; authorisation data would live outside our database.

#### Option 5: Only the operator creates facilities

- Pros: Tightest control; no flag at all.
- Cons: Every facility is set up by hand through the entry point, and the
  right cannot be delegated.

### Running without the real provider

#### Option 1: Mock OIDC server in Compose (chosen)

- Pros: One authentication code path everywhere; the SPA's sign-in flow is
  exercised locally and in staging; no secrets or internet needed.
- Cons: One more container; the mock does not reproduce Entra's exact
  claims, so Entra-specific configuration is verified separately.

#### Option 2: A development authentication scheme in the API

- Pros: No extra container; pick a user with a header.
- Cons: A second code path that must never be enabled in production; the
  SPA's sign-in flow is not exercised outside production.

#### Option 3: The real provider in every environment

- Pros: Highest fidelity.
- Cons: Needs internet and real accounts for every developer and test run;
  staging redirect URIs on the home network need public HTTPS names.

## Consequences

### Positive

- Relatives sign in without passwords, and nobody gets in without an
  invitation from someone already in the family. Only people the operator
  has named can create facilities.
- No credentials, passcode delivery or email sending to run, and nothing
  to pay at this scale.
- The provider is a configuration detail. Our data references only
  `UserId`, so switching provider means re-linking identities, not
  migrating domain data.
- Handlers and domain code are tested with a plain `UserId`. No
  `ClaimsPrincipal` or HTTP type crosses into Application.
- Authorisation rules live in Domain with the rest of the business rules.

### Negative

- Forgetting the access check in a new command handler is not caught by
  the compiler. Each handler needs tests for the non-member and wrong-role
  cases, and reviews must look for the check.
- One extra indexed query per authenticated request to resolve `UserId`.
- Code with no caller, such as scheduled jobs (ADR-0006), has no current
  user. Handlers it runs must not depend on `ICurrentUser`.
- Tokens are held in the browser, so XSS matters more. A strict Content
  Security Policy is required on the frontend.
- An Entra external tenant is a second directory to administer, and part
  of its setup may not be expressible in infrastructure as code.
- Granting or revoking the right to create facilities needs operator
  access to the migration entry point. That is acceptable for the few
  times it happens, but it is not self-service.
- A relative who loses access to their sign-in can be invited again, but
  accepting with a new identity creates a new user. Their stays and
  comments stay with the old one until re-linking an identity to an
  existing user is defined.

### Follow-up

- Configure JWT bearer authentication, the provider key, the fallback
  policy for linked users with its authentication-only opt-out, and
  `ICurrentUser` with its resolving middleware, including the integration
  test token helper
  ([#41](https://github.com/gyback/facility-management/issues/41)).
- Add the `User` aggregate with its `CanCreateFacilities` capability,
  invitations, `AcceptInvitation`, facility roles and the domain
  authorisation checks with the facilities and membership feature
  ([#45](https://github.com/gyback/facility-management/issues/45)).
- Map `UnregisteredUserException` and `ForbiddenException` to their problem
  responses in the error contract, and use its not-found error for anything
  outside the caller's scope
  ([#43](https://github.com/gyback/facility-management/issues/43)).
- Add the mock OIDC server to the Compose stack
  ([#12](https://github.com/gyback/facility-management/issues/12)).
- Add the first-user bootstrap command, and the commands that grant and
  revoke `CanCreateFacilities`, to the migration entry point
  ([#11](https://github.com/gyback/facility-management/issues/11)).
- Create the Entra external tenant and its app registrations, as code where
  the provider allows
  ([#13](https://github.com/gyback/facility-management/issues/13)).
- Decide how a user re-links after losing access to their sign-in, and
  whether a member can remove their own account
  ([#51](https://github.com/gyback/facility-management/issues/51)).

## References

- Issue [#39: Choose how users authenticate and how handlers see the current user](https://github.com/gyback/facility-management/issues/39)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0004: Use a relational database](0004-use-a-relational-database.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [ADR-0006: Deploy to Azure free tiers with the homelab as staging](0006-deploy-to-azure-free-tiers-with-homelab-staging.md)
- [ADR-0007: Keep the deployment portable between Azure and the homelab](0007-keep-the-deployment-target-portable.md)
- [ADR-0011: Use hand-rolled command and query handlers](0011-use-hand-rolled-command-and-query-handlers.md)
- [ADR-0013: Share comment threads across features](0013-share-comment-threads-across-features.md)
- [ADR-0014: Use aggregate repositories for commands and projections for queries](0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md)
- Microsoft, [Microsoft Entra External ID pricing](https://azure.microsoft.com/pricing/details/microsoft-entra-external-id/)
- Microsoft, [Azure AD B2C FAQ](https://learn.microsoft.com/azure/active-directory-b2c/faq)
- IETF, [OAuth 2.0 for Browser-Based Applications](https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/)
- [navikt/mock-oauth2-server](https://github.com/navikt/mock-oauth2-server)
