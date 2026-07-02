# Feature Specification: OIDC Token Introspection Endpoint

**Feature Branch**: `005-introspection-endpoint`

**Created**: 2026-05-22

**Status**: Verified

**Input**: User description: "Implement the OIDC JWT scope and introspection IAM specification."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authenticated APIs introspect tokens (Priority: P1)

An API that receives an access token can ask IAM whether the token is active and receive the subject and authorization metadata needed for fine-grained decisions.

**Why this priority**: This is the core operational flow for APIs that cannot rely only on compact token scopes.

**Independent Test**: Submit an introspection request as an authenticated API client and verify the response reports active token state, subject, and scoped authorization data.

**Acceptance Scenarios**:

1. **Given** an authenticated API caller and an active access token, **When** the API introspects the token, **Then** IAM returns `active: true`, the subject, and relevant permissions.
2. **Given** an unauthenticated caller, **When** it attempts introspection, **Then** IAM rejects the request without exposing token metadata.

---

### User Story 2 - Permissions are filtered by requesting API (Priority: P2)

An API receives only permissions relevant to its own service boundary, even when the user has permissions for several services.

**Why this priority**: Filtering limits data exposure across services and keeps introspection responses narrow.

**Independent Test**: Introspect a token for a user with permissions in multiple service namespaces and verify the caller receives only permissions for its service.

**Acceptance Scenarios**:

1. **Given** a user has patient and inventory permissions, **When** the patient API introspects the user's token, **Then** only patient API permissions are returned.

---

### User Story 3 - Authorization changes are reflected quickly (Priority: P3)

IAM resolves user permissions at introspection time so role changes or removals can affect authorization decisions without waiting for token expiry.

**Why this priority**: Fresh authorization decisions are necessary when permissions change during the lifetime of compact JWTs.

**Independent Test**: Change a user's role permissions after token issuance and verify introspection reflects the current permissions.

**Acceptance Scenarios**:

1. **Given** a user's permission is removed after token issuance, **When** an API introspects the token, **Then** the removed permission is not returned.

### Edge Cases

- Invalid, expired, or revoked tokens return inactive or error responses without permission metadata.
- Missing caller identity prevents permission disclosure.
- Tokens without a user subject can still return caller-filtered token permission claims when present.
- Duplicate permissions are returned once.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: IAM MUST expose an introspection endpoint at `/connect/introspect`.
- **FR-002**: IAM MUST require callers to authenticate before receiving introspection metadata.
- **FR-003**: IAM MUST report whether the token is active.
- **FR-004**: IAM MUST include the token subject for active user tokens when available.
- **FR-005**: IAM MUST return fine-grained permissions only when they are relevant to the authenticated requesting API.
- **FR-006**: IAM SHOULD resolve current user permissions at introspection time rather than relying only on permissions embedded in the token.
- **FR-007**: IAM MUST avoid returning permissions for unrelated APIs.
- **FR-008**: IAM SHOULD apply request rate limiting to introspection requests.

### Key Entities

- **Introspection Request**: The token submitted by an authenticated API caller for validation.
- **Introspection Response**: Active state, subject, and caller-filtered authorization metadata.
- **Permission**: Fine-grained authorization value associated with a service boundary.
- **Requesting API**: The authenticated client asking IAM to introspect a token.

## Security & Operational Impact *(mandatory)*

- **Authentication/Authorization**: Introspection callers must authenticate as API clients; responses are filtered to the caller service.
- **Secrets & Certificates**: Existing confidential client authentication protects callers; no new secret format is introduced.
- **Audit Events**: Existing OpenID Connect and token endpoint monitoring should include introspection activity.
- **Safe Failure Modes**: Invalid or unauthenticated requests must not disclose subject or permissions.
- **Operations**: Introspection requests are rate limited and designed for short-lived caller-side caching.

## Test Strategy *(mandatory)*

- **Unit Tests**: Cover permission filtering, fresh role resolution, fallback token permission claims, and route mapping.
- **API/Integration Tests**: Validate introspection request handling and authenticated caller rejection behavior.
- **Contract Tests**: Not required for Management Web; this is an OAuth/OIDC endpoint.
- **Management Web Tests**: N/A; no Management Web UI changes.
- **Validation Commands**: `dotnet test --project tests/OpenIdentityStack.Api.UnitTests/OpenIdentityStack.Api.UnitTests.csproj --filter-class OpenIdentityStack.Api.UnitTests.Endpoints.OidcControllerRouteTests --no-restore`; `dotnet test --project tests/OpenIdentityStack.Api.Tests/OpenIdentityStack.Api.Tests.csproj --filter-method OpenIdentityStack.Api.Tests.Authentication.AuthorizationControllerTests.Introspect_WhenAuthFails_ReturnsChallenge --filter-method OpenIdentityStack.Api.Tests.Authentication.AuthorizationControllerTests.Introspect_ReturnsActiveSubjectAndCallerFilteredFreshPermissions --no-restore`; `dotnet test --project tests/OpenIdentityStack.Infrastructure.Tests/OpenIdentityStack.Infrastructure.Tests.csproj --filter-class OpenIdentityStack.Infrastructure.Tests.Identity.IntrospectionPermissionsHandlerTests --no-restore`.

## Documentation & Deployment Impact *(mandatory)*

- **Documentation**: Feature specification only.
- **Deployment**: No migration required; existing client registration permissions support introspection for service accounts and clients.
- **Screenshots**: N/A.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Authenticated API callers can introspect active tokens and receive authorization metadata in a single request.
- **SC-002**: Unauthenticated callers receive no subject or permission metadata in 100% of attempts.
- **SC-003**: APIs receive zero permissions outside their service boundary in introspection responses.
- **SC-004**: User role permission changes are reflected on the next successful introspection request.

## Assumptions

- API callers use confidential client authentication or another OpenID Connect-supported client authentication method.
- Service-specific permissions use a service identifier prefix matching the requesting API client identifier.
- Caller-side caching, when used, remains short-lived.

