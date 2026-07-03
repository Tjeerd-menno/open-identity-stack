# Research: Unify Applications Domain

## Decision: Use `Application` as the single top-level domain aggregate

**Rationale**: The current `Client` and `ServiceAccount` aggregates both represent OAuth/OIDC client registrations and own overlapping data such as `client_id`, display name, scopes, grants, and OpenIddict synchronization. A single aggregate removes duplicate invariants, aligns with common IAM product language, and gives administrators one mental model.

**Alternatives considered**:

- Keep both aggregates and share helper services: rejected because business rules, permissions, migration, UI, and credential lifecycle would still be split.
- Rename only the admin UI while keeping the domain split: rejected because it hides rather than fixes projection and validation divergence.
- Use OAuth "client" as the product noun everywhere: rejected because product/admin language should be "Application" while `client_id` remains the protocol identifier.

## Decision: Model service accounts as machine-to-machine applications

**Rationale**: Machine-to-machine access is a profile/capability of an OAuth application, not a separate registration type. `ApplicationProfile.MachineToMachine` captures the current service-account use case and enforces confidential-client and `client_credentials` behavior.

**Alternatives considered**:

- Preserve `ServiceAccount` as a top-level aggregate: rejected by the feature non-goal and because it duplicates OAuth client registration.
- Add a boolean `IsServiceAccount` to clients: rejected because it keeps legacy terminology and underspecifies profile-specific validation.
- Add a separate service principal concept now: rejected as out of scope for delegated tenant/service-principal scenarios.

## Decision: Keep domain-owned credential hashes and lifecycle state

**Rationale**: The domain must be the source of truth for secrets/certificates, status, revocation, expiration, and auditability. OpenIddict remains a protocol projection. This prevents secret rotation from updating only one storage path and avoids split-brain between administrative state and token validation state.

**Alternatives considered**:

- Let OpenIddict fully own secrets: rejected because credential lifecycle and admin audit behavior would be split from the domain.
- Store secrets in both domain and OpenIddict without strict synchronization: rejected because divergence would create security bugs.
- Support only client secrets in this release: rejected because existing service-account certificate behavior must migrate.

## Decision: Generalize service-account validation to application client authentication

**Rationale**: The existing validation handler is scoped to `client_credentials` and service accounts. The new model must validate any confidential application that uses client secret or certificate authentication, reject disabled applications, reject revoked/expired credentials, and update usage metadata.

**Alternatives considered**:

- Keep `ServiceAccountValidationHandler` and add a second handler for applications: rejected because it duplicates authentication logic.
- Depend only on OpenIddict built-in validation: rejected if credential hashes are domain-owned and because disabled/revoked domain state must be authoritative.

## Decision: Remove legacy admin endpoints now

**Rationale**: OpenIdentityStack is still a sub-1.0 pre-release, so breaking changes are allowed. Removing `/api/admin/clients` and `/api/admin/service-accounts` avoids preserving product confusion and eliminates adapter code that would otherwise duplicate route contracts.

**Alternatives considered**:

- Keep compatibility adapters for one release: rejected because compatibility is not a product goal before 1.0.
- Keep old aggregates behind the routes: rejected because it duplicates business rules.
- Maintain compatibility indefinitely: rejected because it preserves product confusion and increases maintenance cost.

## Decision: Use strict migration preflight for production data

**Rationale**: Duplicate `client_id` values or ambiguous legacy combinations can break authentication and administration. Production migrations must fail before mutation and produce actionable remediation details. Service-account grant combinations outside `client_credentials` are not preserved through a compatibility path.

**Alternatives considered**:

- Auto-merge duplicates: rejected because it can silently combine distinct security principals.
- Auto-normalize all invalid grants: rejected because it can change token behavior without operator approval.
- Allow compatibility migration for all environments: rejected because compatibility is not a goal for this pre-1.0 breaking change.

## Decision: Generate new application IDs during migration

**Rationale**: Old `Client.Id` and `ServiceAccount.Id` values do not need to be preserved in a pre-1.0 breaking change. The OAuth `client_id` remains the stable external protocol identifier; `Application.Id` can be generated by the new model.

**Alternatives considered**:

- Preserve IDs where safe: rejected because it adds mapping complexity without a compatibility requirement.
- Use mapping tables for collisions: rejected because legacy internal IDs are not part of the supported external contract.

## Decision: Implement the new admin API contract as `/api/admin/applications`

**Rationale**: A unified resource route makes the product concept visible, supports list filtering by type/status/client type/search, separates metadata updates from OAuth configuration replacement, and isolates credential operations behind stronger permissions.

**Alternatives considered**:

- Reuse `/api/admin/clients`: rejected because it preserves the old product noun and conflicts with the service-account merge.
- Use nested machine-to-machine routes for service-account behavior: rejected because machine-to-machine is a filter/profile, not a separate resource.

## Decision: Use application-specific permissions

**Rationale**: A unified domain concept needs a unified permission namespace. `applications:read`, `applications:write`, `applications:delete`, `applications:manage-credentials`, and `applications:manage-certificates` map clearly to admin actions and replace `clients:*` and `service-accounts:*`.

**Alternatives considered**:

- Reuse `clients:*`: rejected because it does not represent machine-to-machine applications clearly.
- Reuse `service-accounts:*` for machine-to-machine only: rejected because it keeps split authorization.
- Use a single broad `applications:*` only: rejected because credential/certificate actions need separable high-risk permissions.

## Decision: Keep legacy grants (`implicit`, `password`) rejected for new default applications

**Rationale**: The model should align with OAuth security best current practices. Legacy grants may only be handled via explicit legacy/custom migration review, not offered as safe defaults.

**Alternatives considered**:

- Allow legacy grants in `Custom` without feature flags: rejected because it weakens safe-by-default behavior.
- Drop legacy grant data during migration: rejected because it silently changes existing behavior.

## Decision: Update Management Web around an application-profile-first workflow

**Rationale**: Application profile determines safe defaults for grants, redirects, PKCE, consent, and credential behavior. A profile-first wizard reduces misconfiguration and explains the product terms.

**Alternatives considered**:

- Keep separate Clients and Service Accounts pages: rejected because it fails the product simplification goal.
- Use a flat one-page form for all fields: rejected because profile-specific rules are easier to understand in a guided flow.

