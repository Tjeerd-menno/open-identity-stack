# Feature Specification: Current User Permissions

**Feature Branch**: `008-current-user-permissions`

**Created**: 2026-08-07

**Status**: Ready for implementation

**Input**: GitHub issue [#357](https://github.com/Tjeerd-menno/open-identity-stack/issues/357) and accepted grilling decisions from 2026-08-07.

## Governance And Precedence

- This spec fixes the Production regression where encrypted access tokens make Management Web show an empty navigation surface.
- This spec supersedes the portions of `specs/007-management-web` that required Management Web to derive UI permissions from OIDC profile or access-token payload claims.
- Access tokens are credentials presented to the API. Management Web MUST treat them as opaque and MUST NOT depend on whether they are signed JWTs, encrypted JWTs, reference tokens, or another representation.
- Backend authorization remains authoritative. The current-user endpoint only provides client-readable UI authorization state from the already validated access-token principal.

## User Scenarios And Testing

### User Story 1 - Load Management Web permissions with encrypted Production tokens (Priority: P1)

A signed-in operator opens Management Web in Production while the API keeps access-token encryption enabled. Management Web retrieves the current user's effective permissions from the API and renders the expected navigation and permission-gated actions.

**Why this priority**: A correctly provisioned `super-admin` currently sees an empty Management Web menu in Production because the frontend silently fails to decode an encrypted access token.

**Independent Test**: Authenticate with an opaque or encrypted access token representation, mock or issue permission claims on the validated API principal, call `GET /api/me`, and verify Management Web uses the returned permissions rather than decoding the token.

**Acceptance Scenarios**:

1. **Given** Production access-token encryption is enabled, **When** a signed-in Management Web operator calls `GET /api/me`, **Then** the API validates/decrypts the bearer token and returns the current user's effective permissions from the validated `ClaimsPrincipal`.
2. **Given** Management Web receives an opaque token string that JavaScript cannot decode, **When** authentication initialization completes, **Then** Management Web calls `GET /api/me` and uses the returned permissions for navigation and route/action gating.
3. **Given** `/api/me` has not returned yet, **When** Management Web is initializing auth state, **Then** it remains in a loading state rather than rendering an empty permission surface.
4. **Given** `/api/me` fails with `401`, **When** Management Web handles the response, **Then** it follows the existing logout/re-authentication path.
5. **Given** `/api/me` fails for a non-authentication reason, **When** Management Web handles the response, **Then** it surfaces an explicit auth/authorization state error rather than silently showing an empty shell.

### User Story 2 - Refresh UI authorization when the token snapshot changes (Priority: P2)

An operator's access token is refreshed or reissued. Management Web refreshes the current-user representation so its UI permissions match the authorization snapshot represented by the current token.

**Why this priority**: Permission changes should naturally appear when a new access token is issued, without recalculating live permissions only for the frontend.

**Independent Test**: Simulate an access-token value change and verify Management Web refetches `/api/me` and updates permission-gated navigation from the new response.

**Acceptance Scenarios**:

1. **Given** Management Web receives a refreshed access token, **When** the token value changes, **Then** Management Web refetches `GET /api/me`.
2. **Given** `/api/me` returns a changed permission collection, **When** Management Web updates auth state, **Then** route and navigation gating use the new collection.

## Requirements

### Functional Requirements

- **FR-001**: The API MUST expose `GET /api/me`.
- **FR-002**: `GET /api/me` MUST require an authenticated bearer token and MUST NOT require any management permission.
- **FR-003**: `GET /api/me` MUST obtain permissions from the validated access-token `ClaimsPrincipal`; it MUST NOT perform a separate live permission calculation or database lookup.
- **FR-004**: `GET /api/me` MUST read explicit `permission` and `permissions` claims.
- **FR-005**: `GET /api/me` MUST NOT derive product permissions from OAuth `scope` or `scp` claims.
- **FR-006**: `GET /api/me` MUST deduplicate permission strings case-insensitively while preserving first observed spelling and order.
- **FR-007**: `GET /api/me` MUST return wildcard permission strings such as `*` and `users:*` exactly as present in the validated principal.
- **FR-008**: `GET /api/me` MUST return `subject`, `userName`, `displayName`, `email`, and `permissions`.
- **FR-009**: `displayName` fallback order MUST be `name`, `preferred_username`, `email`, then `subject`.
- **FR-010**: `userName` fallback order MUST be `preferred_username`, `name`, `email`, then `subject`.
- **FR-011**: `email` MUST be the email claim value or `null`.
- **FR-012**: If the validated principal has no `sub` or name identifier claim, the endpoint MUST treat the credential as invalid for this API and return `401`.
- **FR-013**: The API SHOULD log anomalous current-user failures such as missing subject, but MUST NOT log every successful `/api/me` call.
- **FR-014**: Management Web MUST stop decoding access-token payloads to determine permissions.
- **FR-015**: Management Web MUST call `GET /api/me` after authentication and when a refreshed/reissued access-token value changes.
- **FR-016**: Management Web MUST keep auth loading active until current-user permissions have loaded or an explicit error state is available.
- **FR-017**: Management Web MUST continue using permission-based route and action gates and MUST NOT infer privileges from role names.
- **FR-018**: `@openidentitystack/admin-api-client` MUST expose a first-class current-user contract and `CurrentUserResponse` type.
- **FR-019**: Contract tests MUST cover the `/api/me` response shape and authenticated-only access requirement.
- **FR-020**: Route mapping tests MUST cover `/api/me` separately from admin permission-policy route tests.
- **FR-021**: Access-token encryption MUST remain enabled by default outside Development/Testing.
- **FR-022**: The solution MUST NOT require exposing permission claims through the ID token.
- **FR-023**: Management Web MUST NOT call `/connect/introspect` directly.

### Key Entities

- **Current User**: The authenticated Management Web user as represented to the frontend by the API, including their effective permission snapshot.
- **Effective Permission Snapshot**: The permission claim set represented by the currently validated access token.
- **Opaque Access Token**: Any access token representation that JavaScript cannot or must not inspect, including encrypted JWT/JWE and reference-token forms.

## Security And Operational Impact

- **Authentication/Authorization**: The endpoint requires an authenticated bearer token only. It does not authorize management operations and does not replace server-side permission enforcement.
- **Token Safety**: Production access-token encryption remains enabled. Management Web no longer depends on token representation or readable token contents.
- **Failure Modes**: Missing subject claims return `401`; frontend failures are explicit rather than silent empty navigation.
- **Observability**: Anomalous current-user failures are logged. Successful startup and refresh calls are not audited or success-logged by default.
- **Revocation Semantics**: Permission changes take effect for the UI when access tokens are refreshed or reissued. Immediate revocation must be solved consistently in API authorization, not only through UI live queries.

## Test Strategy

- **API Unit Tests**: Current-user mapper extracts subject, display fields, email, and permission claims from a `ClaimsPrincipal`; deduplicates permissions; ignores scopes; returns `401` for missing subject.
- **API Route Mapping Tests**: Dedicated test for `GET /api/me`, endpoint name, tag, HTTP method, and authenticated authorization metadata without a named permission policy.
- **Contract Tests**: `/api/me` OpenAPI contract includes response shape and `401`.
- **Frontend Unit/Integration Tests**: Auth provider uses an opaque token, calls `/api/me`, keeps loading until resolved, uses returned permissions, handles `401`, and shows explicit non-401 errors.
- **Frontend API Client Tests**: `createCurrentUserContract` calls `/api/me` and exports `CurrentUserResponse`.
- **Regression Scope**: A full Kubernetes/cert-manager end-to-end test is out of scope for this bugfix. The invariant is covered by backend current-user tests and frontend tests using opaque token strings.

## Documentation And Deployment Impact

- Update `docs/management-web.md` to document `/api/me` as the source of Management Web UI permissions.
- Add an ADR recording why Management Web treats access tokens as opaque and rejects alternatives such as disabling encryption, ID-token permissions, and SPA introspection.
- Update `specs/007-management-web/spec.md` with a supersession note so older parity requirements do not reintroduce token decoding.

## Success Criteria

- **SC-001**: A correctly provisioned `super-admin` sees expected Management Web navigation while the API uses encrypted Production access tokens.
- **SC-002**: Management Web authorization state continues to work when the access token is completely opaque to JavaScript.
- **SC-003**: No Management Web production code decodes access-token payloads to derive permissions.
- **SC-004**: `/api/me` returns the same permission snapshot represented by the current validated access token without an additional database permission lookup.
- **SC-005**: Existing server-side permission enforcement remains unchanged.

## Assumptions

- The OpenIddict validation pipeline already validates and decrypts local encrypted access tokens for API requests.
- Existing permission matching helpers remain the source for wildcard/exact matching behavior in Management Web.
- The endpoint response may expand in the future, but v1 intentionally excludes roles, scopes, and session metadata.
