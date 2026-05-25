# Feature Specification: Unify Applications Domain

**Feature Branch**: `[006-specify-feature]`

**Created**: 2026-05-24

**Status**: Implementing

**Input**: User description: "Unify Clients and Service Accounts into Applications" plus application profile option rules from `application-type-options-matrix.md` and the terminology addendum in `application-profile-spec-addendum.md`

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage one application model (Priority: P1)

As an administrator, I can create and manage one shared application registration model instead of choosing between two separate concepts.

**Why this priority**: This is the core product simplification and unlocks consistent administration and policy.

**Independent Test**: Create, view, update, disable/enable, and delete an application through admin workflows without using any legacy concept.

**Acceptance Scenarios**:

1. **Given** an admin with write permissions, **When** they create an application with a unique client identifier, **Then** the application is saved and can be managed through the unified model.
2. **Given** an existing application, **When** the admin updates descriptive and configuration fields within policy rules, **Then** the changes are persisted and visible in subsequent reads.

---

### User Story 2 - Manage machine-to-machine applications safely (Priority: P2)

As a security administrator, I can configure machine-to-machine applications with safe defaults and manage their credentials lifecycle.

**Why this priority**: Machine-to-machine access is security-sensitive and requires strong credential controls.

**Independent Test**: Create a machine-to-machine application, add and rotate credentials, revoke credentials, and confirm invalid combinations are rejected.

**Acceptance Scenarios**:

1. **Given** a machine-to-machine application, **When** an admin configures it with unsupported interactive grant behavior, **Then** the system rejects the change with a clear validation error.
2. **Given** a confidential application with active credentials, **When** the admin rotates and revokes credentials, **Then** only active credentials continue to authenticate.

---

### User Story 3 - Migrate existing registrations with continuity (Priority: P3)

As a platform operator, I can migrate current client and service-account registrations into applications while preserving external identifiers and minimizing disruption.

**Why this priority**: Migration continuity prevents outage risk for existing consumers.

**Independent Test**: Run migration on existing data, verify identifier preservation and permission mapping, and confirm rollback behavior on duplicate identifiers.

**Acceptance Scenarios**:

1. **Given** legacy registrations with unique client identifiers, **When** migration runs, **Then** each registration is represented as an application with the same client identifier.
2. **Given** duplicate client identifiers across legacy sources, **When** migration preflight runs, **Then** migration fails before data changes and reports conflicts.

---

### User Story 4 - Configure applications through profile-specific policy (Priority: P1)

As an administrator, I can choose an application profile and only see or submit configuration choices that make sense for that profile.

**Why this priority**: The unified model is only safe if product profiles enforce OAuth security rules consistently. The API must be authoritative and the UI should guide administrators away from invalid or insecure combinations before submission.

**Independent Test**: For each supported application profile, request the policy/options matrix, create/configure applications with valid defaults, and verify the API rejects disallowed grants, redirect settings, client profiles, and credential operations while the AdminWeb form hides unavailable options and displays fixed defaults.

**Acceptance Scenarios**:

1. **Given** an admin creating a Single Page application, **When** they configure OAuth settings, **Then** the UI fixes the client profile to public, requires PKCE, allows authorization code and optional refresh tokens only, requires redirect URIs and browser origins, and does not expose secrets or certificates.
2. **Given** an API caller creating a Machine-to-machine application, **When** the request includes redirect URIs, post-logout redirect URIs, refresh tokens, authorization code, or public client profile, **Then** the API rejects the request with a validation error that identifies the policy violation.
3. **Given** an admin creating a Web application, **When** they configure OAuth settings, **Then** the UI defaults to confidential, authorization code, PKCE enabled, and client-secret authentication while treating private-key JWT, mTLS, token lifetime overrides, and similar advanced options as unavailable unless explicitly implemented later.
4. **Given** the reserved Device profile, **When** an admin or API caller attempts to enable Device behavior before the device authorization flow is implemented and tested, **Then** the system blocks creation/configuration or exposes it as unavailable policy metadata rather than a working option.

---

### Edge Cases

- Duplicate client identifiers exist across legacy registration sources.
- A public application is configured with confidential-only credential behavior.
- A machine-to-machine application is configured with redirect-based interactive behavior.
- A Web application is configured with `client_credentials` without an explicit future hybrid-app capability.
- A Single Page or Native application is configured with secrets, certificates, or any confidential token endpoint authentication method.
- A Native application uses redirect URIs that are not valid claimed HTTPS, private-scheme, or loopback redirect patterns.
- A Device application is requested before the device authorization flow is enabled.
- An application is disabled while active token requests are in flight.
- External consumers call removed legacy admin workflows after the breaking change.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide one top-level application concept for all OAuth/OIDC software registrations.
- **FR-002**: The system MUST allow administrators to create, list, view, update, disable, enable, and delete applications.
- **FR-003**: The system MUST preserve the external client identifier as the stable protocol identifier for each application.
- **FR-004**: The system MUST support application classification for at least machine-to-machine and interactive application profiles.
- **FR-005**: The system MUST enforce grant and redirect validation rules based on application profile and client confidentiality.
- **FR-006**: The system MUST allow credential creation, rotation, and revocation for confidential applications.
- **FR-007**: The system MUST reject secret or certificate credential operations for public applications.
- **FR-008**: The system MUST block new token issuance for disabled applications.
- **FR-009**: The system MUST migrate existing client and service-account records into applications.
- **FR-010**: The system MUST preserve existing client identifiers during migration when uniqueness constraints are satisfied.
- **FR-011**: The system MUST detect duplicate client identifiers before migration mutates data and fail safely with actionable reporting.
- **FR-012**: The system MUST migrate existing permissions to a unified applications permission model.
- **FR-013**: The system MUST remove legacy client and service-account admin API routes instead of providing compatibility paths.
- **FR-014**: The system MUST produce audit events for application lifecycle and credential lifecycle changes.
- **FR-015**: The system MUST expose or otherwise define an application-profile policy model that classifies each configuration option as hidden, read-only/fixed, available, or advanced for each application profile.
- **FR-016**: The system MUST enforce application-profile policy rules in API use cases and endpoints, treating UI restrictions as guidance only and never as the source of truth.
- **FR-017**: The system MUST prevent application profile changes after creation unless a future explicit migration workflow is implemented.
- **FR-018**: The system MUST keep advanced options from `application-type-options-matrix.md` as application-profile policy metadata only unless the corresponding protocol behavior is explicitly implemented and tested.
- **FR-019**: The AdminWeb MUST railroad administrators through application-profile-specific choices by hiding unavailable controls, showing fixed defaults as read-only or implicit, and preventing invalid grant/credential/redirect combinations before submission.
- **FR-020**: The system MUST expose the product classification as `ApplicationProfile` in domain/application code and as `profile` in API and AdminWeb contracts, while preserving protocol-level OpenIddict `ApplicationType` naming only inside infrastructure adapters.

### Key Entities *(include if feature involves data)*

- **Application**: Administrator-managed registration that owns identity, profile, status, OAuth behavior, and lifecycle state.
- **Application Credential**: Authentication material (for example secret/certificate) associated with a confidential application, including status and validity metadata.
- **Application Permission Mapping**: Authorization mapping that translates legacy client/service-account permissions into unified application permissions.
- **Application Profile Policy**: Rule set describing default client profile, allowed/default grants, option availability, validation behavior, and exposed `profile` contract semantics for each application profile.

## Security & Operational Impact *(mandatory)*

- **Authentication/Authorization**: Unified application permissions are required for read/write and credential-management actions; legacy permissions are mapped during migration.
- **Secrets & Certificates**: Plain secrets are shown only at creation/rotation time and are never persisted in readable form; revoked/expired credentials cannot authenticate.
- **Audit Events**: Application create/update/disable/enable/delete and credential add/rotate/revoke actions are audit logged with actor and timestamp.
- **Safe Failure Modes**: Migration preflight blocks conflicting data before mutation; invalid configuration requests return explicit validation errors without leaking sensitive details.
- **Policy Enforcement**: API validation rejects disallowed application-profile combinations even if a caller bypasses the AdminWeb.
- **Operations**: Migration is planned for transactional execution where supported, with old admin API routes removed as part of the pre-1.0 breaking change.

## Test Strategy *(mandatory)*

- **Unit Tests**: Validate application invariants (profile/grant/redirect/credential rules), lifecycle transitions, and permission mapping behavior.
- **API/Integration Tests**: Validate end-to-end admin workflows, migration behavior, disable-token-block behavior, credential lifecycle flows, API rejection of every disallowed profile policy combination, and the `profile` request/response shape.
- **Contract Tests**: Validate unified admin contract behavior, application-profile policy response shape, `profile` request/query/response naming, and ensure removed legacy admin routes are not part of the supported contract.
- **AdminWeb Tests**: Validate application-centric terminology, machine-to-machine workflows, credential management UX behavior, and profile-specific railroaded form behavior.
- **Validation Commands**: Run the repository’s existing backend, integration, contract, and admin web test commands used by CI.

## Documentation & Deployment Impact *(mandatory)*

- **Documentation**: Update administrator-facing terminology and migration guidance to use "Application", "Application Profile", and "Machine-to-machine application" as primary product terms.
- **Deployment**: Requires coordinated schema migration, permission mapping rollout, and communication that legacy admin API routes have been removed.
- **Screenshots**: Required for admin UI changes that replace legacy labels and update application management flows.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of new admin-created OAuth/OIDC registrations are created through the unified application model.
- **SC-002**: At least 95% of existing client/service-account records migrate automatically without manual intervention in a clean dataset.
- **SC-003**: 100% of duplicate-client-identifier conflicts are detected before mutation and reported with actionable conflict details.
- **SC-004**: 100% of disabled applications are prevented from obtaining new tokens after disablement.
- **SC-005**: Support tickets related to "client vs service account confusion" decrease by at least 50% within one release after rollout.
- **SC-006**: 100% of application create/configure API requests that violate the profile policy matrix fail with deterministic validation errors.
- **SC-007**: 100% of AdminWeb application creation/configuration paths hide or disable unavailable options for the selected application profile.
- **SC-008**: 100% of supported Applications API request, response, filter, and OpenAPI surfaces expose the product classification as `profile` rather than `type`.

## Assumptions

- Existing administrators already have role-based access to the current client and service-account admin workflows.
- This is a sub-1.0 pre-release breaking change; legacy client and service-account admin API routes are removed now.
- Machine-to-machine applications are limited to non-interactive grant behavior in this release.
- Advanced matrix options such as private-key JWT, mTLS, JWKS, DPoP, token lifetime overrides, and confidential Device behavior are metadata/reserved unless explicitly implemented in a later feature.
- Existing external integrations depend on stable client identifiers that must remain unchanged through migration.
