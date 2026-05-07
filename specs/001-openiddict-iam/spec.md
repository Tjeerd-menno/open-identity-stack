# Feature Specification: OpenIddict-Based Identity & Access Management

**Feature Branch**: `001-openiddict-iam`  
**Created**: 2026-01-18  
**Status**: Draft  
**Input**: User Requirements Specification for IAM solution supporting local users, federated users, groups, Admin API, service accounts, session management, and Single Logout

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Local User Authentication (Priority: P1)

A local user authenticates with their username/email and password to obtain tokens for accessing protected applications.

**Why this priority**: Core authentication is the foundational capability. Without local user login, no other features can be demonstrated or tested. This enables the most basic IAM functionality.

**Independent Test**: Can be fully tested by creating a local user, performing login via authorization code flow, and validating the issued tokens contain correct claims.

**Acceptance Scenarios**:

1. **Given** a registered local user with valid credentials, **When** they authenticate via the authorization endpoint with correct password, **Then** they receive a valid authorization code that can be exchanged for tokens.
2. **Given** a registered local user, **When** they provide an incorrect password, **Then** authentication fails with a generic error (not revealing whether the username exists).
3. **Given** a disabled local user, **When** they attempt to authenticate with correct credentials, **Then** authentication is denied.
4. **Given** credentials are stored in persistence, **When** an administrator inspects the data store, **Then** passwords are stored only as salted hashes (no plaintext).

---

### User Story 2 - Service Account Token Acquisition (Priority: P2)

A service account (machine client) authenticates using client credentials (secret or certificate) to obtain access tokens for calling protected APIs.

**Why this priority**: Machine-to-machine authentication enables automated systems and background services to integrate with the IAM. This is essential for microservices architectures and system integrations.

**Independent Test**: Can be tested by registering a service account with client credentials, calling the token endpoint with valid credentials, and validating the issued access token.

**Acceptance Scenarios**:

1. **Given** a registered service account with valid client secret, **When** it requests a token via client credentials grant, **Then** an access token is issued with configured scopes.
2. **Given** a registered service account with valid client certificate, **When** it presents the certificate during token request, **Then** an access token is issued.
3. **Given** invalid client credentials, **When** a token request is made, **Then** the request is rejected with an appropriate error.
4. **Given** a disabled service account, **When** it attempts to obtain a token, **Then** the request is denied.

---

### User Story 3 - Admin User Management (Priority: P3)

A system administrator creates, updates, and manages local users through the Admin API, including enabling/disabling users and resetting passwords.

**Why this priority**: User lifecycle management enables ongoing operations. Administrators need to onboard new users, handle account issues, and maintain user status. This is required before scaling to production use.

**Independent Test**: Can be tested by calling Admin API endpoints to create a user, query the user, disable the user, and verify the user cannot authenticate while disabled.

**Acceptance Scenarios**:

1. **Given** an authenticated administrator, **When** they create a local user via Admin API, **Then** the user appears in the user list with correct status.
2. **Given** an existing user, **When** an administrator disables the user via Admin API, **Then** the user cannot authenticate until re-enabled.
3. **Given** an existing user, **When** an administrator resets their password via Admin API, **Then** the user can authenticate with the new credentials.
4. **Given** an existing user, **When** an administrator deletes the user via Admin API, **Then** the user no longer appears in queries and cannot authenticate.

---

### User Story 4 - Federated User Login via Upstream IdP (Priority: P4)

An end user authenticates via an upstream OIDC identity provider (e.g., Microsoft Entra ID, Keycloak) and receives tokens from this IAM for accessing relying party applications.

**Why this priority**: Federation enables enterprise SSO scenarios and allows organizations to leverage existing identity infrastructure. This is a key differentiator for enterprise adoption.

**Independent Test**: Can be tested by configuring an upstream IdP, initiating login, authenticating at the upstream IdP, and verifying the IAM issues tokens to the relying party.

**Acceptance Scenarios**:

1. **Given** an upstream OIDC provider is configured, **When** a user initiates login and selects the upstream provider, **Then** they are redirected to the upstream IdP for authentication.
2. **Given** a user authenticates successfully at the upstream IdP, **When** redirected back to the IAM, **Then** the IAM issues a valid authorization code to the relying party.
3. **Given** an upstream user logs in for the first time, **When** authentication succeeds, **Then** a local user representation is created (JIT provisioning) with upstream identifiers stored.
4. **Given** the same upstream user logs in again, **When** authentication succeeds, **Then** the existing local representation is reused (no duplicate records).

---

### User Story 5 - Role-Based Access Control (Priority: P5)

A system administrator assigns roles to users (local or federated), and those roles are included in the tokens issued to relying parties for authorization decisions.

**Why this priority**: Role-based access enables relying parties to make authorization decisions based on user roles. This is essential for any access control beyond simple authentication.

**Independent Test**: Can be tested by creating a role, assigning it to a user, having the user authenticate, and verifying the issued token contains the role claim.

**Acceptance Scenarios**:

1. **Given** a role exists, **When** an administrator assigns it to a user via Admin API, **Then** the user's record reflects the role assignment.
2. **Given** a user has assigned roles, **When** they obtain a token, **Then** the token contains the role claims as configured.
3. **Given** a role is removed from a user, **When** they obtain a new token, **Then** the removed role is not present in the token.
4. **Given** an upstream user is provisioned, **When** an administrator assigns a role to them, **Then** subsequent tokens include that role.

---

### User Story 6 - Group Management and Group-Based Authorization (Priority: P6)

A system administrator creates groups, assigns users to groups, and configures group-to-role and group-to-claim mappings so that group membership drives authorization claims in issued tokens.

**Why this priority**: Groups provide scalable authorization management. Instead of assigning roles individually, administrators can manage permissions at the group level, reducing administrative overhead and enabling organizational alignment.

**Independent Test**: Can be tested by creating a group with role mappings, adding a user to the group, authenticating as that user, and verifying the token contains the mapped roles/claims.

**Acceptance Scenarios**:

1. **Given** a group exists, **When** a user is added to the group via Admin API, **Then** the membership is persisted and visible in user detail.
2. **Given** a group-to-role mapping is configured, **When** a user in that group obtains a token, **Then** the mapped roles appear in the token as effective roles.
3. **Given** a group-to-claim mapping is configured, **When** a user in that group obtains a token, **Then** the mapped claims appear in the configured token type (access token and/or ID token).
4. **Given** a user is removed from a group, **When** they obtain a new token, **Then** group-derived roles and claims are no longer present.
5. **Given** hierarchical groups are enabled and a user is in a child group, **When** they obtain a token, **Then** effective membership includes parent group(s) according to configuration.

---

### User Story 7 - Admin Service Account Management (Priority: P7)

A system administrator creates and manages service accounts (clients) through the Admin API, including configuring client secrets, certificates, and allowed scopes.

**Why this priority**: Service account management enables administrators to onboard and maintain machine clients. This is required for production deployments with multiple integrating systems.

**Independent Test**: Can be tested by calling Admin API to create a service account, configure its credentials, and verify the service account can obtain tokens.

**Acceptance Scenarios**:

1. **Given** an authenticated administrator, **When** they create a service account via Admin API, **Then** the service account can authenticate and obtain tokens.
2. **Given** an existing service account, **When** an administrator rotates its client secret, **Then** the old secret stops working and the new secret works.
3. **Given** an existing service account, **When** an administrator configures a client certificate, **Then** the service account can authenticate using that certificate.
4. **Given** an existing service account, **When** an administrator disables it, **Then** token requests are rejected.

---

### User Story 8 - Admin Upstream Identity Management (Priority: P8)

A system administrator links or unlinks upstream identities to local user representations for account consolidation or remediation scenarios.

**Why this priority**: Identity linking enables administrators to consolidate accounts and handle edge cases where automatic JIT provisioning is insufficient. This supports advanced identity governance scenarios.

**Independent Test**: Can be tested by linking an upstream identity to an existing user, having that upstream user authenticate, and verifying they are recognized as the linked user.

**Acceptance Scenarios**:

1. **Given** an existing local user, **When** an administrator links an upstream identity (issuer + subject), **Then** that upstream user logging in is recognized as the linked local user.
2. **Given** a linked upstream identity, **When** an administrator unlinks it, **Then** subsequent logins from that upstream identity either create a new record (JIT enabled) or are blocked (JIT disabled).

---

### User Story 9 - Session Management and Visibility (Priority: P9)

A system administrator can view active user sessions and revoke them when needed for security or compliance purposes.

**Why this priority**: Session visibility and revocation are critical for security incident response and compliance. Administrators must be able to terminate compromised sessions.

**Independent Test**: Can be tested by having a user authenticate, querying active sessions via Admin API, revoking a session, and verifying subsequent requests using that session's tokens are handled appropriately.

**Acceptance Scenarios**:

1. **Given** a user has authenticated and has an active session, **When** an administrator queries sessions via Admin API, **Then** the session is visible with relevant metadata (user, client, login time).
2. **Given** an active session exists, **When** an administrator revokes the session via Admin API, **Then** refresh token requests for that session fail.
3. **Given** a user has multiple active sessions, **When** an administrator revokes all sessions for that user, **Then** all refresh token requests for that user fail.

---

### User Story 10 - Single Logout (SLO) (Priority: P10)

When a user logs out from one application, other applications participating in the session are notified to terminate their local sessions, providing a coordinated logout experience.

**Why this priority**: Single Logout improves security by ensuring sessions are terminated across all applications when a user logs out. This reduces the window of opportunity for session hijacking.

**Independent Test**: Can be tested by having a user authenticate to multiple clients, initiating logout from one client, and verifying other clients receive logout notifications (front-channel or back-channel as configured).

**Acceptance Scenarios**:

1. **Given** a user has active sessions with multiple relying party clients, **When** the user initiates logout from one client, **Then** the IAM terminates the IAM session and initiates logout notifications to other clients as configured.
2. **Given** front-channel logout is configured for a client, **When** logout occurs, **Then** the client's front-channel logout URI is invoked via the user's browser.
3. **Given** back-channel logout is configured for a client, **When** logout occurs, **Then** the IAM sends a logout token to the client's back-channel logout endpoint.
4. **Given** a client does not support SLO, **When** logout occurs, **Then** that client is skipped without blocking the logout flow.

---

### User Story 11 - Delegated Administration (Priority: P11)

An IAM Super Admin can create delegated admin roles with limited permissions, allowing role-specific administrators (e.g., User Admin, Role Admin) to perform their duties without full system access.

**Why this priority**: Delegated administration follows the principle of least privilege. Organizations need to distribute administrative responsibilities without granting full access to all administrators.

**Independent Test**: Can be tested by creating a delegated admin with user management permissions only, verifying they can manage users but cannot manage roles or service accounts.

**Acceptance Scenarios**:

1. **Given** a delegated admin role with user management permissions, **When** that admin attempts to create a user, **Then** the operation succeeds.
2. **Given** a delegated admin role with user management permissions only, **When** that admin attempts to create a role, **Then** the operation is denied (403).
3. **Given** a super admin, **When** they create a delegated admin with specific permissions, **Then** the delegated admin can only perform operations matching those permissions.

---

### Edge Cases

- What happens when an upstream IdP is unreachable during federated login?
  - User should see a meaningful error and be able to retry or choose alternative authentication methods.
- What happens when a user's session expires during an interactive flow?
  - User should be prompted to re-authenticate without losing context where possible.
- How does the system handle duplicate email addresses across upstream providers?
  - The system should use (issuer, subject) as the unique identifier, not email, to prevent account hijacking.
- What happens when an administrator deletes a user with active sessions?
  - Active access tokens remain valid until expiration (stateless validation), but refresh token requests should fail and sessions should be terminated.
- How does the system handle clock skew between IAM and relying parties?
  - Tokens should include standard tolerances and relying parties should be advised on acceptable clock skew.
- What happens when conflicting group-to-role mappings exist?
  - Deterministic precedence rules are applied and documented; roles are unioned (additive) by default.
- What happens when a back-channel logout endpoint is unreachable?
  - The logout flow should continue; failures should be logged but not block the user's logout.

## Requirements *(mandatory)*

### Functional Requirements

#### Core Identity Provider

- **FR-001**: System MUST implement OIDC Provider and OAuth2 Authorization Server capabilities
- **FR-002**: System MUST expose `/.well-known/openid-configuration` endpoint returning valid OIDC discovery metadata
- **FR-003**: System MUST expose JWKS endpoint returning active public signing keys
- **FR-004**: System MUST support Authorization Code with PKCE flow for interactive clients
- **FR-005**: System MUST reject authorization code requests from public clients that do not include PKCE
- **FR-006**: System MUST support Client Credentials flow for service accounts
- **FR-007**: System MUST issue JWT access tokens that can be validated using the published JWKS
- **FR-008**: System SHOULD support refresh tokens with configurable rotation policy

#### Local Users

- **FR-010**: System MUST support local user authentication using username/email and password
- **FR-011**: System MUST store passwords only as salted hashes (no plaintext)
- **FR-012**: System MUST return generic error messages that do not reveal whether a username exists
- **FR-013**: System MUST support local user lifecycle operations: create, disable/enable, password reset, delete

#### Federation

- **FR-020**: System MUST allow configuration of one or more upstream OIDC identity providers
- **FR-021**: System MUST reject upstream provider configuration when discovery metadata is unreachable
- **FR-022**: System MUST support federated login where users authenticate at upstream IdP and receive tokens from this IAM
- **FR-023**: System MUST support Just-In-Time (JIT) provisioning for upstream users on first login
- **FR-024**: System MUST store stable upstream identifiers (issuer + subject) to uniquely identify federated users
- **FR-025**: System MUST support claim mapping rules to transform upstream claims to issued token claims
- **FR-026**: System MUST allow administrators to link/unlink upstream identities to local user representations

#### Roles and Authorization

- **FR-030**: System MUST support a role model with create/update/delete role operations
- **FR-031**: System MUST allow role assignment/unassignment to users (local or federated)
- **FR-032**: System MUST include assigned role claims in issued tokens (configurable claim type)
- **FR-033**: System MUST allow administrators to assign roles to upstream users independently of upstream group membership

#### Groups and Group Mappings

- **FR-040**: System MUST support a group model with create/update/delete group operations
- **FR-041**: System MUST allow user assignment/unassignment to groups (local or federated users)
- **FR-042**: System SHOULD support hierarchical groups (nested groups), configurable per deployment
- **FR-043**: System MUST support configurable group-to-role mapping rules
- **FR-044**: System MUST support configurable group-to-claim mapping rules
- **FR-045**: System MUST apply group mappings consistently for both local and upstream users
- **FR-046**: System MUST apply deterministic precedence rules when conflicting mappings exist

#### Service Accounts

- **FR-050**: System MUST support service account (machine client) authentication via client secret
- **FR-051**: System MUST support service account authentication via client certificate
- **FR-052**: System MUST allow configuration of allowed scopes per service account

#### Session Management

- **FR-060**: System MUST maintain user sessions for interactive authentication flows
- **FR-061**: System MUST allow administrators to query active sessions via Admin API
- **FR-062**: System MUST allow administrators to revoke individual sessions or all sessions for a user
- **FR-063**: System MUST reject refresh token requests for revoked sessions

#### Single Logout (SLO)

- **FR-070**: System MUST support OIDC front-channel logout for configured clients
- **FR-071**: System MUST support OIDC back-channel logout for configured clients
- **FR-072**: System MUST continue logout flow when individual client logout endpoints fail
- **FR-073**: System MUST log SLO failures for operational visibility

#### Admin API

- **FR-080**: System MUST provide Admin API for managing users (local and federated representations)
- **FR-081**: System MUST provide Admin API for managing groups and group membership
- **FR-082**: System MUST provide Admin API for managing roles and role assignments
- **FR-083**: System MUST provide Admin API for managing upstream identity links
- **FR-084**: System MUST provide Admin API for managing service accounts (clients)
- **FR-085**: System MUST provide Admin API for managing credentials (client secrets, certificate metadata)
- **FR-086**: System MUST provide Admin API for session visibility and revocation
- **FR-087**: Admin API MUST require authentication via OAuth2 access tokens issued by the IAM
- **FR-088**: Admin API MUST enforce role-based access control (RBAC)
- **FR-089**: Admin API MUST log all security-relevant operations for audit

#### Delegated Administration

- **FR-090**: System MUST support delegated admin roles with configurable permission sets
- **FR-091**: System MUST enforce permission boundaries for delegated admins
- **FR-092**: System MUST allow super admins to create and manage delegated admin roles

### Key Entities

- **User**: Represents a person who can authenticate. Has identity attributes (name, email), credentials (for local users), status (enabled/disabled), role assignments, and group memberships. May be linked to zero or more upstream identities.

- **Upstream Identity**: Represents a link between a User and an external identity provider. Identified by (issuer, subject) pair. Stores claim mapping context.

- **Role**: Represents a named permission grouping that can be assigned directly to users or derived from group membership. Contains name, description, and can be referenced in token claims.

- **Group**: Represents a collection of users for organizational and authorization purposes. Can have role mappings and claim mappings. May support hierarchical relationships (parent/child groups).

- **Service Account (Client)**: Represents a non-human client entity. Has client identifier, credentials (secret and/or certificate), allowed scopes, and status.

- **Upstream Provider Configuration**: Represents configuration for an external OIDC identity provider. Contains authority URL, client credentials, scope configuration, and claim mapping rules.

- **Session**: Represents an authenticated user session at the IAM for interactive flows. Contains user reference, client references, login timestamp, and status.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete local authentication flow in under 3 seconds end-to-end
- **SC-002**: System supports 1,000 concurrent authentication requests without degradation
- **SC-003**: Token validation by relying parties succeeds 100% of the time for non-expired, properly-signed tokens
- **SC-004**: 95% of first-time federated users are successfully provisioned (JIT) without manual intervention
- **SC-005**: Administrators can create a new user via Admin API in under 5 seconds
- **SC-006**: Role and group assignment changes are reflected in newly issued tokens within 1 authentication cycle
- **SC-007**: Service accounts can obtain tokens in under 500ms
- **SC-008**: System passes security audit with no critical or high vulnerabilities related to authentication flows
- **SC-009**: All Admin API operations are logged and auditable within compliance requirements
- **SC-010**: System achieves 99.9% uptime for authentication endpoints during normal operations
- **SC-011**: Session revocation takes effect within 5 seconds for refresh token operations
- **SC-012**: Single Logout notifications are sent to 99% of configured clients within 10 seconds

## Assumptions

- The solution will be deployed in an environment with network access to configured upstream identity providers
- Relying parties can validate JWTs using standard libraries and the published JWKS endpoint
- Administrators have appropriate tooling to call the Admin API (e.g., CLI, scripts, or integration with management systems)
- Clock synchronization between the IAM and relying parties is maintained within acceptable tolerances (±5 minutes recommended)
- Initial deployment will not require SCIM or HR provisioning integration (can be added later)
- A full end-user self-service portal is out of scope; only Admin API is required
- Fine-grained authorization policy engine (ABAC) beyond roles, groups, and claim mappings is out of scope unless explicitly added
- Clients supporting SLO will implement standard OIDC logout endpoints

## Out of Scope

- Full end-user UI/portal (admin UI optional; API is required)
- SCIM-based HR provisioning integration
- Full ABAC / policy engine beyond roles and group mapping
- Password complexity policy configuration (use reasonable defaults)
- Social identity providers (Google, Facebook, etc.) - focus is enterprise OIDC federation
- Multi-Factor Authentication (MFA/TOTP) - deferred to future iteration
