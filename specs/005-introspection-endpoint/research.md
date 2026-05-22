# Research: OIDC Token Introspection Endpoint

## Decision: Use OpenIddict's Built-In Introspection Pipeline

**Rationale**: OpenIddict already owns caller authentication, endpoint permissions, token validation, token storage checks, and inactive-token behavior. Keeping those responsibilities in OpenIddict reduces security risk and avoids duplicating OAuth 2.0 introspection semantics in custom HTTP code.

**Alternatives considered**:
- Custom controller-only implementation: rejected because it risks bypassing OpenIddict client authentication and token activity semantics.
- Separate internal API endpoint: rejected because the spec requires `/connect/introspect` and OAuth 2.0 caller expectations.

## Decision: Enrich Successful Responses with a Scoped Server Event Handler

**Rationale**: OpenIddict 7.5 exposes server events for introspection response handling, while ASP.NET Core passthrough is not available for introspection. A scoped event handler can add authorization metadata after OpenIddict has validated the caller and token.

**Alternatives considered**:
- ASP.NET Core passthrough for introspection: rejected because the current OpenIddict ASP.NET Core builder does not expose an introspection passthrough method.
- Mutating token issuance to embed all permissions: rejected because the feature goal is compact JWTs and fresh authorization data.

## Decision: Resolve Fresh User Permissions from Existing Effective Role Query

**Rationale**: `IGetUserEffectiveRolesQueryHandler` already centralizes direct and group-mapped role resolution. Using it during introspection lets role changes affect the next introspection response without waiting for JWT expiry.

**Alternatives considered**:
- Use only token `permission` claims: rejected for user tokens because it does not satisfy freshness requirements.
- Add a new permission store/query: rejected because the existing effective roles query already provides current permissions.

## Decision: Filter Permissions by Requesting Client Identifier

**Rationale**: The spec models service scopes and service-specific permissions by service/API boundary. Filtering permissions to values prefixed with the authenticated caller client id keeps unrelated API permissions out of the response.

**Alternatives considered**:
- Return all permissions: rejected because it violates the disclosure boundary.
- Filter by token audience only: deferred because existing registered permissions already use service-prefixed keys and caller client id is available at introspection time.

## Decision: No New Storage or Package Dependencies

**Rationale**: Existing OpenIddict token/application storage, user/role persistence, and ASP.NET Core rate limiting are sufficient for the feature. Avoiding new dependencies keeps the implementation aligned with constitution package constraints.

**Alternatives considered**:
- Add distributed cache for introspection responses: deferred to future work because caller-side short-lived caching is sufficient for this scope.
- Add policy engine integration: deferred to future IAM extensions.
